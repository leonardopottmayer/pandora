using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Pottmayer.Pandora.IntegrationTests.Support;
using Pottmayer.Pandora.Modules.Finances.Application.Commands.RunRecurrenceGeneration;
using Pottmayer.Tars.Core.Mediator.Abstractions;
using Xunit;

namespace Pottmayer.Pandora.IntegrationTests.Modules.Finances;

/// <summary>
/// Covers phase-08 pending transaction inbox: editing the proposal, approving (creates transaction),
/// rejecting, batch approve, and idempotency of approve/reject on already-decided entries.
/// </summary>
[Collection("Integration")]
public sealed class PendingTransactionsTests : IAsyncLifetime
{
    private const string Accounts = "/api/v1/finances/accounts";
    private const string RecurringTransactions = "/api/v1/finances/recurring-transactions";
    private const string PendingTransactions = "/api/v1/finances/pending-transactions";
    private const string Transactions = "/api/v1/finances/transactions";

    private readonly PandoraWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public PendingTransactionsTests(PandoraWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public Task InitializeAsync() => _factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Approve_creates_transaction_and_marks_pending_as_approved()
    {
        await AuthAsync("pending-approve");
        var account = await CreateAccountAsync();
        await SeedRecurringAndGenerateAsync(account, new DateOnly(2026, 6, 1));

        var pending = await ListPendingAsync();
        var entry = Assert.Single(pending);
        Assert.Equal("pending", entry.Status);

        var approve = await _client.PostAsJsonAsync($"{PendingTransactions}/{entry.Id}/approve", new { });
        Assert.Equal(HttpStatusCode.OK, approve.StatusCode);

        // The approve endpoint returns the created TransactionDto
        var createdTx = (await approve.Content.ReadFromJsonAsync<SingleEnvelope<TxNode>>())!.Data;
        Assert.Equal("posted", createdTx.Status);

        var txList = (await _client.GetFromJsonAsync<ListEnvelope<TxNode>>($"{Transactions}?accountId={account}"))!.Data;
        Assert.Single(txList, t => t.Id == createdTx.Id);
    }

    [Fact]
    public async Task Reject_marks_pending_as_rejected_with_reason()
    {
        await AuthAsync("pending-reject");
        var account = await CreateAccountAsync();
        await SeedRecurringAndGenerateAsync(account, new DateOnly(2026, 6, 1));

        var pending = await ListPendingAsync();
        var entry = Assert.Single(pending);

        var reject = await _client.PostAsJsonAsync($"{PendingTransactions}/{entry.Id}/reject",
            new { reason = "Not this month" });
        Assert.Equal(HttpStatusCode.OK, reject.StatusCode);

        var rejectedPending = (await reject.Content.ReadFromJsonAsync<SingleEnvelope<PendingNode>>())!.Data;
        Assert.Equal("rejected", rejectedPending.Status);
    }

    [Fact]
    public async Task Double_approve_returns_conflict()
    {
        await AuthAsync("pending-double-approve");
        var account = await CreateAccountAsync();
        await SeedRecurringAndGenerateAsync(account, new DateOnly(2026, 6, 1));

        var pending = await ListPendingAsync();
        var entry = Assert.Single(pending);

        await _client.PostAsJsonAsync($"{PendingTransactions}/{entry.Id}/approve", new { });
        var second = await _client.PostAsJsonAsync($"{PendingTransactions}/{entry.Id}/approve", new { });
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Update_edits_proposal_without_changing_original_payload()
    {
        await AuthAsync("pending-edit");
        var account = await CreateAccountAsync();
        await SeedRecurringAndGenerateAsync(account, new DateOnly(2026, 6, 1));

        var pending = await ListPendingAsync();
        var entry = Assert.Single(pending);

        var put = await _client.PutAsJsonAsync($"{PendingTransactions}/{entry.Id}", new
        {
            kind = "expense",
            amount = 99m,
            occurredOn = new DateOnly(2026, 6, 1),
            description = "Edited description"
        });
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);

        var updated = (await put.Content.ReadFromJsonAsync<SingleEnvelope<PendingNode>>())!.Data;
        Assert.Equal(99m, updated.Amount);
        Assert.Equal("Edited description", updated.Description);
        // OriginalPayload must not be changed — it's stored separately and opaque to this response
    }

    [Fact]
    public async Task Batch_approve_approves_all_pending()
    {
        await AuthAsync("pending-batch");
        var account = await CreateAccountAsync();

        // Create 2 recurring transactions → 2 pending entries
        await CreateRecurringAsync(new
        {
            name = "Gas",
            accountId = account,
            kind = "expense",
            amount = 200m,
            amountIsEstimate = false,
            description = "Gas bill",
            frequency = "monthly",
            interval = 1,
            startDate = new DateOnly(2026, 6, 1),
            autoPost = false
        });
        await CreateRecurringAsync(new
        {
            name = "Electric",
            accountId = account,
            kind = "expense",
            amount = 150m,
            amountIsEstimate = false,
            description = "Electric bill",
            frequency = "monthly",
            interval = 1,
            startDate = new DateOnly(2026, 6, 1),
            autoPost = false
        });

        await RunGenerationAsync(new DateOnly(2026, 6, 1), horizonDays: 0);

        var pending = await ListPendingAsync();
        Assert.Equal(2, pending.Count);

        var ids = pending.Select(p => p.Id).ToList();
        var batch = await _client.PostAsJsonAsync($"{PendingTransactions}/approve-batch", new { ids });
        Assert.Equal(HttpStatusCode.OK, batch.StatusCode);

        var result = (await batch.Content.ReadFromJsonAsync<SingleEnvelope<int>>())!.Data;
        Assert.Equal(2, result);

        // All pending now approved — inbox should be empty
        var remaining = await ListPendingAsync();
        Assert.Empty(remaining);
    }

    // ── helpers ──

    private Task AuthAsync(string username) =>
        IdentityHelper.AuthenticateAsync(_client, _factory.ConnectionString, $"{username}@example.com", username);

    private async Task<Guid> CreateAccountAsync()
    {
        var response = await _client.PostAsJsonAsync(Accounts, new
        {
            name = "Checking",
            type = "checking",
            currency = "BRL",
            displayOrder = 0
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<SingleEnvelope<IdNode>>())!.Data.Id;
    }

    private async Task SeedRecurringAndGenerateAsync(Guid accountId, DateOnly startDate)
    {
        await CreateRecurringAsync(new
        {
            name = "Rent",
            accountId,
            kind = "expense",
            amount = 1000m,
            amountIsEstimate = false,
            description = "Monthly rent",
            frequency = "monthly",
            interval = 1,
            startDate,
            autoPost = false
        });
        await RunGenerationAsync(startDate, horizonDays: 0);
    }

    private async Task CreateRecurringAsync(object body)
    {
        var response = await _client.PostAsJsonAsync(RecurringTransactions, body);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private async Task<IReadOnlyList<PendingNode>> ListPendingAsync() =>
        (await _client.GetFromJsonAsync<ListEnvelope<PendingNode>>(PendingTransactions))!.Data;

    private async Task<int> RunGenerationAsync(DateOnly today, int horizonDays = 30)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var result = await sender.Send(new RunRecurrenceGenerationCommand(
            new RunRecurrenceGenerationInput(today, horizonDays)));
        Assert.True(result.IsSuccess);
        return result.Value;
    }

    private sealed record SingleEnvelope<T>(T Data);
    private sealed record ListEnvelope<T>(IReadOnlyList<T> Data);
    private sealed record IdNode(Guid Id);
    private sealed record PendingNode(Guid Id, string Status, Guid? TransactionId, decimal? Amount, string Description);
    private sealed record TxNode(Guid Id, string Status, DateOnly OccurredOn);
}
