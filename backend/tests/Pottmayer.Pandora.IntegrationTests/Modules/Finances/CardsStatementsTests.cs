using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Pottmayer.Pandora.IntegrationTests.Support;
using Pottmayer.Pandora.Modules.Finances.Application.Commands.RunStatementLifecycle;
using Pottmayer.Tars.Core.Mediator.Abstractions;
using Xunit;

namespace Pottmayer.Pandora.IntegrationTests.Modules.Finances;

[Collection("Integration")]
public sealed class CardsStatementsTests : IAsyncLifetime
{
    private const string Accounts = "/api/v1/finances/accounts";
    private const string Cards = "/api/v1/finances/cards";
    private const string Transactions = "/api/v1/finances/transactions";
    private const string Statements = "/api/v1/finances/statements";

    private readonly PandoraWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public CardsStatementsTests(PandoraWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public Task InitializeAsync() => _factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Closing_day_boundary_and_december_rollover_land_in_different_statements()
    {
        await AuthAsync("card-boundary");
        var card = await CreateCardAsync(new
        {
            name = "Nubank",
            closingDay = 28,
            dueDay = 5,
            currency = "BRL",
            creditLimit = 1000m
        });

        var a = await CreateCardTxAsync(new { cardId = card, kind = "expense", amount = 100m, occurredOn = new DateOnly(2026, 12, 28), description = "A" });
        var b = await CreateCardTxAsync(new { cardId = card, kind = "expense", amount = 50m, occurredOn = new DateOnly(2026, 12, 29), description = "B" });

        Assert.NotEqual(a.CardStatementId, b.CardStatementId);

        var statements = await _client.GetFromJsonAsync<ListEnvelope<CardStatementNode>>($"{Cards}/{card}/statements");
        Assert.Contains(statements!.Data, s => s.ReferenceMonth == "2026-12");
        Assert.Contains(statements.Data, s => s.ReferenceMonth == "2027-01");
    }

    [Fact]
    public async Task Lifecycle_job_is_idempotent()
    {
        await AuthAsync("card-lifecycle");
        var card = await CreateCardAsync(new
        {
            name = "Visa",
            closingDay = 10,
            dueDay = 20,
            currency = "BRL"
        });

        await CreateCardTxAsync(new { cardId = card, kind = "expense", amount = 100m, occurredOn = new DateOnly(2026, 6, 5), description = "Lunch" });

        await RunLifecycleAsync(new DateOnly(2026, 6, 10));
        await RunLifecycleAsync(new DateOnly(2026, 6, 10));

        var statements = (await _client.GetFromJsonAsync<ListEnvelope<CardStatementNode>>($"{Cards}/{card}/statements"))!.Data;
        Assert.Equal(statements.Count, statements.Select(s => s.ReferenceMonth).Distinct().Count());
        Assert.Contains(statements, s => s.ReferenceMonth == "2026-06" && s.Status == "closed");
    }

    [Fact]
    public async Task Partial_and_total_payment_update_status_and_debit_account_balance()
    {
        await AuthAsync("card-payment");
        var account = await CreateAccountAsync(new { name = "Checking", type = "checking", currency = "BRL", displayOrder = 0, openingBalance = 1000m });
        var card = await CreateCardAsync(new
        {
            name = "Master",
            closingDay = 10,
            dueDay = 20,
            currency = "BRL",
            defaultPaymentAccountId = account
        });

        var tx = await CreateCardTxAsync(new { cardId = card, kind = "expense", amount = 120m, occurredOn = new DateOnly(2026, 6, 5), description = "Market" });
        Assert.Equal(HttpStatusCode.OK, (await _client.PostAsync($"{Statements}/{tx.CardStatementId}/close", null)).StatusCode);

        var partial = await _client.PostAsJsonAsync($"{Statements}/{tx.CardStatementId}/pay", new { accountId = account, amount = 50m });
        Assert.Equal(HttpStatusCode.OK, partial.StatusCode);
        var partialStatement = (await partial.Content.ReadFromJsonAsync<SingleEnvelope<CardStatementNode>>())!.Data;
        Assert.Equal("partially-paid", partialStatement.Status);

        var balanceAfterPartial = await _client.GetFromJsonAsync<SingleEnvelope<BalanceNode>>($"{Accounts}/{account}/balance");
        Assert.Equal(950m, balanceAfterPartial!.Data.Posted);

        var total = await _client.PostAsJsonAsync($"{Statements}/{tx.CardStatementId}/pay", new { accountId = account, amount = 70m });
        Assert.Equal(HttpStatusCode.OK, total.StatusCode);
        var paidStatement = (await total.Content.ReadFromJsonAsync<SingleEnvelope<CardStatementNode>>())!.Data;
        Assert.Equal("paid", paidStatement.Status);

        var balanceAfterPaid = await _client.GetFromJsonAsync<SingleEnvelope<BalanceNode>>($"{Accounts}/{account}/balance");
        Assert.Equal(880m, balanceAfterPaid!.Data.Posted);
    }

    [Fact]
    public async Task Available_limit_uses_unpaid_statements()
    {
        await AuthAsync("card-limit");
        var card = await CreateCardAsync(new
        {
            name = "Limit",
            closingDay = 10,
            dueDay = 20,
            currency = "BRL",
            creditLimit = 1000m
        });

        await CreateCardTxAsync(new { cardId = card, kind = "expense", amount = 200m, occurredOn = new DateOnly(2026, 6, 5), description = "A" });
        await CreateCardTxAsync(new { cardId = card, kind = "expense", amount = 150m, occurredOn = new DateOnly(2026, 6, 15), description = "B" });

        var limit = await _client.GetFromJsonAsync<SingleEnvelope<LimitNode>>($"{Cards}/{card}/available-limit");
        Assert.Equal(650m, limit!.Data.AvailableLimit);
    }

    [Fact]
    public async Task Refund_reduces_statement_total_and_detail_matches_cache()
    {
        await AuthAsync("card-refund");
        var card = await CreateCardAsync(new
        {
            name = "Refund",
            closingDay = 10,
            dueDay = 20,
            currency = "BRL"
        });

        var expense = await CreateCardTxAsync(new { cardId = card, kind = "expense", amount = 100m, occurredOn = new DateOnly(2026, 6, 5), description = "Purchase" });
        await CreateCardTxAsync(new { cardId = card, kind = "refund", amount = 25m, occurredOn = new DateOnly(2026, 6, 6), description = "Refund" });

        var statement = await _client.GetFromJsonAsync<SingleEnvelope<StatementDetailNode>>($"{Statements}/{expense.CardStatementId}");
        Assert.Equal(75m, statement!.Data.Statement.TotalAmount);
        Assert.Equal(2, statement.Data.Transactions.Count);

        var statements = await _client.GetFromJsonAsync<ListEnvelope<CardStatementNode>>($"{Cards}/{card}/statements");
        Assert.Contains(statements!.Data, s => s.Id == expense.CardStatementId && s.TotalAmount == 75m);
    }

    [Fact]
    public async Task Reopening_closed_statement_restores_open_status()
    {
        await AuthAsync("card-reopen-closed");
        var card = await CreateCardAsync(new { name = "Card", closingDay = 28, dueDay = 5, currency = "BRL", creditLimit = 1000m });
        var tx = await CreateCardTxAsync(new { cardId = card, kind = "expense", amount = 80m, occurredOn = new DateOnly(2026, 6, 5), description = "Purchase" });

        Assert.Equal(HttpStatusCode.OK, (await _client.PostAsync($"{Statements}/{tx.CardStatementId}/close", null)).StatusCode);

        var reopenResponse = await _client.PostAsync($"{Statements}/{tx.CardStatementId}/reopen", null);
        Assert.Equal(HttpStatusCode.OK, reopenResponse.StatusCode);
        var reopened = (await reopenResponse.Content.ReadFromJsonAsync<SingleEnvelope<CardStatementNode>>())!.Data;
        Assert.Equal("open", reopened.Status);
    }

    [Fact]
    public async Task Reopening_open_statement_returns_conflict()
    {
        await AuthAsync("card-reopen-open");
        var card = await CreateCardAsync(new { name = "Card", closingDay = 28, dueDay = 5, currency = "BRL", creditLimit = 1000m });
        var tx = await CreateCardTxAsync(new { cardId = card, kind = "expense", amount = 80m, occurredOn = new DateOnly(2026, 6, 5), description = "Purchase" });

        var response = await _client.PostAsync($"{Statements}/{tx.CardStatementId}/reopen", null);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Reopening_paid_statement_returns_conflict()
    {
        await AuthAsync("card-reopen-paid");
        var account = await CreateAccountAsync(new { name = "Checking", type = "checking", currency = "BRL", displayOrder = 0, openingBalance = 500m });
        var card = await CreateCardAsync(new { name = "Card", closingDay = 28, dueDay = 5, currency = "BRL", defaultPaymentAccountId = account });
        var tx = await CreateCardTxAsync(new { cardId = card, kind = "expense", amount = 80m, occurredOn = new DateOnly(2026, 6, 5), description = "Purchase" });

        Assert.Equal(HttpStatusCode.OK, (await _client.PostAsync($"{Statements}/{tx.CardStatementId}/close", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _client.PostAsJsonAsync($"{Statements}/{tx.CardStatementId}/pay", new { accountId = account, amount = 80m, occurredOn = new DateOnly(2026, 6, 20) })).StatusCode);

        var response = await _client.PostAsync($"{Statements}/{tx.CardStatementId}/reopen", null);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    private Task AuthAsync(string username) =>
        IdentityHelper.AuthenticateAsync(_client, _factory.ConnectionString, $"{username}@example.com", username);

    private async Task<Guid> CreateAccountAsync(object body)
    {
        var response = await _client.PostAsJsonAsync(Accounts, body);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<SingleEnvelope<AccountNode>>())!.Data.Id;
    }

    private async Task<Guid> CreateCardAsync(object body)
    {
        var response = await _client.PostAsJsonAsync(Cards, body);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<SingleEnvelope<CardNode>>())!.Data.Id;
    }

    private async Task<TransactionNode> CreateCardTxAsync(object body)
    {
        var response = await _client.PostAsJsonAsync(Transactions, body);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<SingleEnvelope<TransactionNode>>())!.Data;
    }

    private async Task RunLifecycleAsync(DateOnly today)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var result = await sender.Send(new RunStatementLifecycleCommand(new RunStatementLifecycleInput(today)));
        Assert.True(result.IsSuccess);
    }

    private sealed record SingleEnvelope<T>(T Data);
    private sealed record ListEnvelope<T>(IReadOnlyList<T> Data);
    private sealed record AccountNode(Guid Id);
    private sealed record CardNode(Guid Id);
    private sealed record TransactionNode(Guid Id, Guid? CardStatementId);
    private sealed record CardStatementNode(Guid Id, string ReferenceMonth, string Status, decimal TotalAmount);
    private sealed record BalanceNode(Guid AccountId, string Currency, decimal Posted, decimal Projected);
    private sealed record LimitNode(Guid CardId, decimal? CreditLimit, decimal? AvailableLimit);
    private sealed record StatementDetailNode(CardStatementNode Statement, IReadOnlyList<TransactionNode> Transactions);
}
