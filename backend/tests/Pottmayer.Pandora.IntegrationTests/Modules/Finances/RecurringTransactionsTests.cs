using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Pottmayer.Pandora.IntegrationTests.Support;
using Pottmayer.Pandora.Modules.Finances.Application.Commands.RunRecurrenceGeneration;
using Pottmayer.Tars.Core.Mediator.Abstractions;
using Xunit;

namespace Pottmayer.Pandora.IntegrationTests.Modules.Finances;

/// <summary>
/// Covers phase-08 recurring transactions: CRUD, pause/resume, generation job producing pending
/// transactions or direct postings, idempotency, and termination.
/// </summary>
[Collection("Integration")]
public sealed class RecurringTransactionsTests : IAsyncLifetime
{
    private const string Accounts = "/api/v1/finances/accounts";
    private const string Cards = "/api/v1/finances/cards";
    private const string RecurringTransactions = "/api/v1/finances/recurring-transactions";
    private const string PendingTransactions = "/api/v1/finances/pending-transactions";
    private const string Transactions = "/api/v1/finances/transactions";

    private readonly PandoraWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public RecurringTransactionsTests(PandoraWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public Task InitializeAsync() => _factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Create_list_and_delete_recurring_transaction()
    {
        await AuthAsync("rec-crud");
        var account = await CreateAccountAsync();
        var startDate = new DateOnly(2026, 7, 1);

        var created = await CreateRecurringAsync(new
        {
            name = "Rent",
            accountId = account,
            kind = "expense",
            amount = 1500m,
            amountIsEstimate = false,
            description = "Monthly rent",
            frequency = "monthly",
            interval = 1,
            startDate = startDate,
            autoPost = false
        });

        Assert.Equal("Rent", created.Name);
        Assert.Equal("active", created.Status);
        Assert.Equal(0, created.OccurrencesCount);

        var list = await ListRecurringAsync();
        Assert.Single(list, r => r.Id == created.Id);

        var get = await GetRecurringAsync(created.Id);
        Assert.Equal(created.Id, get.Id);

        var del = await _client.DeleteAsync($"{RecurringTransactions}/{created.Id}");
        Assert.Equal(HttpStatusCode.OK, del.StatusCode);

        var listAfter = await ListRecurringAsync();
        Assert.DoesNotContain(listAfter, r => r.Id == created.Id);
    }

    [Fact]
    public async Task Update_changes_mutable_fields()
    {
        await AuthAsync("rec-update");
        var account = await CreateAccountAsync();

        var created = await CreateRecurringAsync(new
        {
            name = "Streaming",
            accountId = account,
            kind = "expense",
            amount = 50m,
            amountIsEstimate = false,
            description = "Subscription",
            frequency = "monthly",
            interval = 1,
            startDate = new DateOnly(2026, 1, 1),
            autoPost = false
        });

        var put = await _client.PutAsJsonAsync($"{RecurringTransactions}/{created.Id}", new
        {
            name = "Netflix",
            amount = 55m,
            amountIsEstimate = false,
            description = "Monthly streaming",
            autoPost = false
        });
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);

        var updated = (await put.Content.ReadFromJsonAsync<SingleEnvelope<RecurringTxNode>>())!.Data;
        Assert.Equal("Netflix", updated.Name);
        Assert.Equal(55m, updated.Amount);
        Assert.Equal("monthly", updated.Frequency); // immutable
    }

    [Fact]
    public async Task Pause_and_resume_change_status()
    {
        await AuthAsync("rec-pause");
        var account = await CreateAccountAsync();

        var r = await CreateRecurringAsync(new
        {
            name = "Internet",
            accountId = account,
            kind = "expense",
            amount = 100m,
            amountIsEstimate = false,
            description = "ISP",
            frequency = "monthly",
            interval = 1,
            startDate = new DateOnly(2026, 1, 1),
            autoPost = false
        });

        var paused = await _client.PostAsJsonAsync($"{RecurringTransactions}/{r.Id}/pause", new { });
        Assert.Equal(HttpStatusCode.OK, paused.StatusCode);
        var pausedNode = (await paused.Content.ReadFromJsonAsync<SingleEnvelope<RecurringTxNode>>())!.Data;
        Assert.Equal("paused", pausedNode.Status);

        // Pausing twice returns conflict
        var pauseAgain = await _client.PostAsJsonAsync($"{RecurringTransactions}/{r.Id}/pause", new { });
        Assert.Equal(HttpStatusCode.Conflict, pauseAgain.StatusCode);

        var resumed = await _client.PostAsJsonAsync($"{RecurringTransactions}/{r.Id}/resume", new { });
        Assert.Equal(HttpStatusCode.OK, resumed.StatusCode);
        var resumedNode = (await resumed.Content.ReadFromJsonAsync<SingleEnvelope<RecurringTxNode>>())!.Data;
        Assert.Equal("active", resumedNode.Status);
    }

    [Fact]
    public async Task Generation_job_creates_pending_transactions_for_account_without_autopost()
    {
        await AuthAsync("rec-gen-inbox");
        var account = await CreateAccountAsync();
        var startDate = new DateOnly(2026, 6, 1);

        await CreateRecurringAsync(new
        {
            name = "Gym",
            accountId = account,
            kind = "expense",
            amount = 80m,
            amountIsEstimate = false,
            description = "Monthly gym",
            frequency = "monthly",
            interval = 1,
            startDate = startDate,
            autoPost = false
        });

        // Run generation for a horizon that includes startDate
        var count = await RunGenerationAsync(new DateOnly(2026, 6, 1), horizonDays: 0);
        Assert.True(count >= 1);

        var pending = await ListPendingAsync();
        Assert.Contains(pending, p => p.Description == "Monthly gym" && p.OccurredOn == startDate);
    }

    [Fact]
    public async Task Generation_job_auto_posts_to_account_when_autoPost_is_true()
    {
        await AuthAsync("rec-gen-autopost");
        var account = await CreateAccountAsync();
        var startDate = new DateOnly(2026, 6, 1);

        await CreateRecurringAsync(new
        {
            name = "Salary",
            accountId = account,
            kind = "income",
            amount = 5000m,
            amountIsEstimate = false,
            description = "Monthly salary",
            frequency = "monthly",
            interval = 1,
            startDate = startDate,
            autoPost = true
        });

        await RunGenerationAsync(new DateOnly(2026, 6, 1), horizonDays: 0);

        // auto_post = true for account → direct transaction, no pending entry
        var pending = await ListPendingAsync();
        Assert.DoesNotContain(pending, p => p.Description == "Monthly salary");

        var txList = (await _client.GetFromJsonAsync<ListEnvelope<TxNode>>($"{Transactions}?accountId={account}"))!.Data;
        Assert.Contains(txList, t => t.Description == "Monthly salary" && t.OccurredOn == startDate);
    }

    [Fact]
    public async Task Generation_job_is_idempotent()
    {
        await AuthAsync("rec-gen-idempotent");
        var account = await CreateAccountAsync();

        await CreateRecurringAsync(new
        {
            name = "Water",
            accountId = account,
            kind = "expense",
            amount = 30m,
            amountIsEstimate = false,
            description = "Water bill",
            frequency = "monthly",
            interval = 1,
            startDate = new DateOnly(2026, 6, 1),
            autoPost = false
        });

        await RunGenerationAsync(new DateOnly(2026, 6, 1), horizonDays: 0);
        await RunGenerationAsync(new DateOnly(2026, 6, 1), horizonDays: 0);

        var pending = await ListPendingAsync();
        Assert.Equal(1, pending.Count(p => p.Description == "Water bill"));
    }

    [Fact]
    public async Task Recurrence_finishes_after_max_occurrences()
    {
        await AuthAsync("rec-gen-max");
        var account = await CreateAccountAsync();

        var r = await CreateRecurringAsync(new
        {
            name = "OneShot",
            accountId = account,
            kind = "expense",
            amount = 10m,
            amountIsEstimate = false,
            description = "One-time recurring",
            frequency = "monthly",
            interval = 1,
            startDate = new DateOnly(2026, 6, 1),
            maxOccurrences = 1,
            autoPost = false
        });

        await RunGenerationAsync(new DateOnly(2026, 6, 1), horizonDays: 31);

        var refreshed = await GetRecurringAsync(r.Id);
        Assert.Equal("finished", refreshed.Status);
        Assert.Equal(1, refreshed.OccurrencesCount);
    }

    [Fact]
    public async Task Generation_for_card_always_goes_to_inbox()
    {
        await AuthAsync("rec-gen-card");
        var card = await CreateCardAsync();
        var startDate = new DateOnly(2026, 6, 15);

        await CreateRecurringAsync(new
        {
            name = "Netflix",
            cardId = card,
            kind = "expense",
            amount = 30m,
            amountIsEstimate = false,
            description = "Streaming subscription",
            frequency = "monthly",
            interval = 1,
            startDate = startDate,
            autoPost = true  // autoPost=true is ignored for cards
        });

        await RunGenerationAsync(new DateOnly(2026, 6, 15), horizonDays: 0);

        var pending = await ListPendingAsync();
        Assert.Contains(pending, p => p.Description == "Streaming subscription");
    }

    [Fact]
    public async Task Generation_job_skips_recurrence_with_autoGenerate_false()
    {
        await AuthAsync("rec-gen-noauto");
        var account = await CreateAccountAsync();
        var startDate = new DateOnly(2026, 6, 1);

        await CreateRecurringAsync(new
        {
            name = "ManualOnly",
            accountId = account,
            kind = "expense",
            amount = 80m,
            amountIsEstimate = false,
            description = "Manual only bill",
            frequency = "monthly",
            interval = 1,
            startDate = startDate,
            autoPost = false,
            autoGenerate = false
        });

        await RunGenerationAsync(new DateOnly(2026, 6, 1), horizonDays: 0);

        var pending = await ListPendingAsync();
        Assert.DoesNotContain(pending, p => p.Description == "Manual only bill");
    }

    [Fact]
    public async Task Manual_generation_to_inbox_creates_pending_and_advances_cursor()
    {
        await AuthAsync("rec-manual-inbox");
        var account = await CreateAccountAsync();
        var startDate = new DateOnly(2026, 6, 1);

        var r = await CreateRecurringAsync(new
        {
            name = "Gym",
            accountId = account,
            kind = "expense",
            amount = 80m,
            amountIsEstimate = false,
            description = "Monthly gym",
            frequency = "monthly",
            interval = 1,
            startDate = startDate,
            autoPost = false,
            autoGenerate = false
        });

        var response = await _client.PostAsJsonAsync($"{RecurringTransactions}/{r.Id}/generate", new
        {
            destination = "inbox",
            description = "Gym (manual)",
            amount = 95m
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var generated = (await response.Content.ReadFromJsonAsync<SingleEnvelope<GeneratedNode>>())!.Data;
        Assert.Equal("inbox", generated.Destination);
        Assert.NotNull(generated.Pending);

        var pending = await ListPendingAsync();
        Assert.Contains(pending, p => p.Description == "Gym (manual)" && p.OccurredOn == startDate);

        // cursor advanced one month, occurrence counted
        var refreshed = await GetRecurringAsync(r.Id);
        Assert.Equal(1, refreshed.OccurrencesCount);
    }

    [Fact]
    public async Task Manual_generation_to_transactions_posts_directly()
    {
        await AuthAsync("rec-manual-tx");
        var account = await CreateAccountAsync();
        var startDate = new DateOnly(2026, 6, 1);

        var r = await CreateRecurringAsync(new
        {
            name = "Salary",
            accountId = account,
            kind = "income",
            amount = 5000m,
            amountIsEstimate = false,
            description = "Monthly salary",
            frequency = "monthly",
            interval = 1,
            startDate = startDate,
            autoPost = false,
            autoGenerate = false
        });

        var overrideDate = new DateOnly(2026, 6, 5);
        var response = await _client.PostAsJsonAsync($"{RecurringTransactions}/{r.Id}/generate", new
        {
            destination = "transactions",
            occurredOn = overrideDate
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var generated = (await response.Content.ReadFromJsonAsync<SingleEnvelope<GeneratedNode>>())!.Data;
        Assert.Equal("transactions", generated.Destination);
        Assert.NotNull(generated.Transaction);

        // posted to official transactions with the overridden date; inbox untouched
        var txList = (await _client.GetFromJsonAsync<ListEnvelope<TxNode>>($"{Transactions}?accountId={account}"))!.Data;
        Assert.Contains(txList, t => t.Description == "Monthly salary" && t.OccurredOn == overrideDate);

        var pending = await ListPendingAsync();
        Assert.DoesNotContain(pending, p => p.Description == "Monthly salary");
    }

    [Fact]
    public async Task Manual_generation_without_advance_keeps_schedule()
    {
        await AuthAsync("rec-manual-noadvance");
        var account = await CreateAccountAsync();
        var startDate = new DateOnly(2026, 6, 1);

        var r = await CreateRecurringAsync(new
        {
            name = "Gym",
            accountId = account,
            kind = "expense",
            amount = 80m,
            amountIsEstimate = false,
            description = "Monthly gym",
            frequency = "monthly",
            interval = 1,
            startDate = startDate,
            autoPost = false,
            autoGenerate = false
        });

        // Generate twice for the same occurrence without advancing the schedule. Official transactions
        // carry no per-occurrence uniqueness, so duplicates are allowed.
        for (var i = 0; i < 2; i++)
        {
            var response = await _client.PostAsJsonAsync($"{RecurringTransactions}/{r.Id}/generate", new
            {
                destination = "transactions",
                advanceSchedule = false
            });
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        var txList = (await _client.GetFromJsonAsync<ListEnvelope<TxNode>>($"{Transactions}?accountId={account}"))!.Data;
        Assert.Equal(2, txList.Count(t => t.Description == "Monthly gym" && t.OccurredOn == startDate));

        // schedule untouched: still at the first occurrence, nothing counted
        var refreshed = await GetRecurringAsync(r.Id);
        Assert.Equal(0, refreshed.OccurrencesCount);
    }

    [Fact]
    public async Task Manual_generation_to_inbox_rejects_duplicate_occurrence()
    {
        await AuthAsync("rec-manual-inbox-dup");
        var account = await CreateAccountAsync();
        var startDate = new DateOnly(2026, 6, 1);

        var r = await CreateRecurringAsync(new
        {
            name = "Gym",
            accountId = account,
            kind = "expense",
            amount = 80m,
            amountIsEstimate = false,
            description = "Monthly gym",
            frequency = "monthly",
            interval = 1,
            startDate = startDate,
            autoPost = false,
            autoGenerate = false
        });

        var first = await _client.PostAsJsonAsync($"{RecurringTransactions}/{r.Id}/generate", new
        {
            destination = "inbox",
            advanceSchedule = false
        });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        // Same recurrence + date in the inbox violates idempotency → clean conflict, not a 500.
        var second = await _client.PostAsJsonAsync($"{RecurringTransactions}/{r.Id}/generate", new
        {
            destination = "inbox",
            advanceSchedule = false
        });
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Manual_generation_to_transactions_for_card_creates_statement_and_posts()
    {
        await AuthAsync("rec-manual-card-tx");
        var card = await CreateCardAsync();
        var startDate = new DateOnly(2026, 6, 15);

        var r = await CreateRecurringAsync(new
        {
            name = "Netflix",
            cardId = card,
            kind = "expense",
            amount = 30m,
            amountIsEstimate = false,
            description = "Streaming subscription",
            frequency = "monthly",
            interval = 1,
            startDate = startDate,
            autoPost = false,
            autoGenerate = false
        });

        var response = await _client.PostAsJsonAsync($"{RecurringTransactions}/{r.Id}/generate", new
        {
            destination = "transactions"
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var generated = (await response.Content.ReadFromJsonAsync<SingleEnvelope<GeneratedNode>>())!.Data;
        Assert.Equal("transactions", generated.Destination);
        Assert.NotNull(generated.Transaction);

        var txList = (await _client.GetFromJsonAsync<ListEnvelope<TxNode>>($"{Transactions}?cardId={card}"))!.Data;
        Assert.Contains(txList, t => t.Description == "Streaming subscription" && t.OccurredOn == startDate);
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

    private async Task<Guid> CreateCardAsync()
    {
        var response = await _client.PostAsJsonAsync(Cards, new
        {
            name = "Nubank",
            closingDay = 28,
            dueDay = 5,
            currency = "BRL"
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<SingleEnvelope<IdNode>>())!.Data.Id;
    }

    private async Task<RecurringTxNode> CreateRecurringAsync(object body)
    {
        var response = await _client.PostAsJsonAsync(RecurringTransactions, body);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<SingleEnvelope<RecurringTxNode>>())!.Data;
    }

    private async Task<IReadOnlyList<RecurringTxNode>> ListRecurringAsync() =>
        (await _client.GetFromJsonAsync<ListEnvelope<RecurringTxNode>>(RecurringTransactions))!.Data;

    private async Task<RecurringTxNode> GetRecurringAsync(Guid id) =>
        (await _client.GetFromJsonAsync<SingleEnvelope<RecurringTxNode>>($"{RecurringTransactions}/{id}"))!.Data;

    private async Task<IReadOnlyList<PendingTxNode>> ListPendingAsync() =>
        (await _client.GetFromJsonAsync<ListEnvelope<PendingTxNode>>(PendingTransactions))!.Data;

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
    private sealed record RecurringTxNode(Guid Id, string Name, string Status, string Frequency, decimal? Amount, int OccurrencesCount);
    private sealed record PendingTxNode(Guid Id, string Description, DateOnly OccurredOn, string Status);
    private sealed record TxNode(Guid Id, string Description, DateOnly OccurredOn);
    private sealed record GeneratedNode(string Destination, PendingTxNode? Pending, TxNode? Transaction);
}
