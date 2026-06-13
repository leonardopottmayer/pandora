using System.Net;
using System.Net.Http.Json;
using Pottmayer.Pandora.IntegrationTests.Support;
using Xunit;

namespace Pottmayer.Pandora.IntegrationTests.Modules.Finances;

/// <summary>
/// Covers the phase-04 ledger: balance derived from posted entries (with the correct sign per kind),
/// scheduled (pending) entries, transfers (same- and cross-currency, atomic void of the pair),
/// cosmetic-only editing with audit diff, the statement filters/paging, and archived-account
/// rejection. Maps to the five acceptance criteria of roadmap/04-lancamentos.md.
/// </summary>
[Collection("Integration")]
public sealed class TransactionsTests : IAsyncLifetime
{
    private const string Accounts = "/api/v1/finances/accounts";
    private const string Transactions = "/api/v1/finances/transactions";
    private const string AuditUrl = "/api/v1/finances/audit";

    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);

    private readonly PandoraWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public TransactionsTests(PandoraWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public Task InitializeAsync() => _factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Balance_is_opening_plus_signed_posted_entries()
    {
        await AuthAsync("tx-balance");
        var account = await CreateAccountAsync(new { name = "Wallet", type = "cash", currency = "BRL", displayOrder = 0, openingBalance = 100m });

        await CreateTxAsync(new { accountId = account, kind = "income", amount = 50m, occurredOn = Today, description = "Salary" });
        await CreateTxAsync(new { accountId = account, kind = "expense", amount = 30m, occurredOn = Today, description = "Groceries" });
        await CreateTxAsync(new { accountId = account, kind = "adjustment", amount = 10m, occurredOn = Today, description = "Fix" });

        var balance = await GetBalanceAsync(account);
        Assert.Equal(130m, balance.Posted);   // 100 + 50 - 30 + 10
        Assert.Equal(130m, balance.Projected);
    }

    [Fact]
    public async Task Investment_kinds_are_restricted_to_investment_accounts()
    {
        await AuthAsync("tx-invest");
        var checking = await CreateAccountAsync(new { name = "Checking", type = "checking", currency = "BRL", displayOrder = 0 });
        var fund = await CreateAccountAsync(new { name = "Fund", type = "investment", currency = "BRL", displayOrder = 1 });

        var rejected = await PostTxAsync(new { accountId = checking, kind = "yield", amount = 5m, occurredOn = Today, description = "Yield" });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, rejected);

        var ok = await PostTxAsync(new { accountId = fund, kind = "yield", amount = 5m, occurredOn = Today, description = "Yield" });
        Assert.Equal(HttpStatusCode.OK, ok);
    }

    [Fact]
    public async Task Scheduled_entry_is_pending_and_posts_on_demand()
    {
        await AuthAsync("tx-schedule");
        var account = await CreateAccountAsync(new { name = "Wallet", type = "cash", currency = "BRL", displayOrder = 0, openingBalance = 100m });

        var future = Today.AddMonths(1);
        var scheduled = await CreateTxAsync(new { accountId = account, kind = "expense", amount = 40m, occurredOn = future, description = "Rent" });
        Assert.Equal("pending", scheduled.Status);

        var balance = await GetBalanceAsync(account);
        Assert.Equal(100m, balance.Posted);      // pending excluded
        Assert.Equal(60m, balance.Projected);    // pending included

        Assert.Equal(HttpStatusCode.OK, (await _client.PostAsync($"{Transactions}/{scheduled.Id}/post", null)).StatusCode);

        var afterPost = await GetBalanceAsync(account);
        Assert.Equal(60m, afterPost.Posted);
    }

    [Fact]
    public async Task Cross_currency_transfer_records_both_amounts_and_void_cancels_the_pair()
    {
        await AuthAsync("tx-fx");
        var brl = await CreateAccountAsync(new { name = "BRL", type = "checking", currency = "BRL", displayOrder = 0, openingBalance = 1000m });
        var usd = await CreateAccountAsync(new { name = "USD", type = "international", currency = "USD", displayOrder = 1 });

        var response = await _client.PostAsJsonAsync($"{Transactions}/transfer", new
        {
            fromAccountId = brl,
            toAccountId = usd,
            amountOut = 100m,
            amountIn = 20m,
            fxRate = 0.2m,
            occurredOn = Today,
            description = "Buy USD"
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var legs = (await response.Content.ReadFromJsonAsync<ListEnvelope>())!.Data;
        Assert.Equal(2, legs.Count);
        Assert.All(legs, l => Assert.NotNull(l.TransferGroupId));
        Assert.Contains(legs, l => l is { Kind: "transfer-out", Amount: 100m, Currency: "BRL" });
        Assert.Contains(legs, l => l is { Kind: "transfer-in", Amount: 20m, Currency: "USD", FxRate: 0.2m });

        Assert.Equal(900m, (await GetBalanceAsync(brl)).Posted);
        Assert.Equal(20m, (await GetBalanceAsync(usd)).Posted);

        // Voiding either leg cancels both.
        var oneLeg = legs[0].Id;
        Assert.Equal(HttpStatusCode.OK, (await _client.PostAsync($"{Transactions}/{oneLeg}/void", null)).StatusCode);

        Assert.Equal(1000m, (await GetBalanceAsync(brl)).Posted);
        Assert.Equal(0m, (await GetBalanceAsync(usd)).Posted);
    }

    [Fact]
    public async Task Cross_currency_transfer_without_both_amounts_is_rejected()
    {
        await AuthAsync("tx-fx-bad");
        var brl = await CreateAccountAsync(new { name = "BRL", type = "checking", currency = "BRL", displayOrder = 0, openingBalance = 1000m });
        var usd = await CreateAccountAsync(new { name = "USD", type = "international", currency = "USD", displayOrder = 1 });

        var response = await _client.PostAsJsonAsync($"{Transactions}/transfer", new
        {
            fromAccountId = brl,
            toAccountId = usd,
            amountOut = 100m,
            occurredOn = Today,
            description = "Buy USD"
        });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task Same_currency_transfer_mirrors_the_amount()
    {
        await AuthAsync("tx-transfer");
        var a = await CreateAccountAsync(new { name = "A", type = "checking", currency = "BRL", displayOrder = 0, openingBalance = 500m });
        var b = await CreateAccountAsync(new { name = "B", type = "savings", currency = "BRL", displayOrder = 1 });

        var response = await _client.PostAsJsonAsync($"{Transactions}/transfer", new
        {
            fromAccountId = a,
            toAccountId = b,
            amountOut = 200m,
            occurredOn = Today,
            description = "Move"
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        Assert.Equal(300m, (await GetBalanceAsync(a)).Posted);
        Assert.Equal(200m, (await GetBalanceAsync(b)).Posted);
    }

    [Fact]
    public async Task Edit_is_cosmetic_and_audited_with_a_diff()
    {
        await AuthAsync("tx-edit");
        var account = await CreateAccountAsync(new { name = "Wallet", type = "cash", currency = "BRL", displayOrder = 0 });
        var tx = await CreateTxAsync(new { accountId = account, kind = "expense", amount = 30m, occurredOn = Today, description = "Old" });

        var response = await _client.PutAsJsonAsync($"{Transactions}/{tx.Id}", new { description = "New", payee = "Shop" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = (await response.Content.ReadFromJsonAsync<SingleEnvelope>())!.Data;
        Assert.Equal("New", updated.Description);
        Assert.Equal(30m, updated.Amount);          // value untouched by a cosmetic edit

        var audit = await _client.GetFromJsonAsync<AuditEnvelope>($"{AuditUrl}?entityType=transaction&entityId={tx.Id}");
        Assert.Contains("transaction.created", audit!.Data.Select(e => e.EventType));
        Assert.Contains("transaction.edited", audit.Data.Select(e => e.EventType));
    }

    [Fact]
    public async Task Statement_filters_and_pages_stably()
    {
        await AuthAsync("tx-extrato");
        var account = await CreateAccountAsync(new { name = "Wallet", type = "cash", currency = "BRL", displayOrder = 0 });

        await CreateTxAsync(new { accountId = account, kind = "income", amount = 10m, occurredOn = Today.AddDays(-2), description = "UBER trip" });
        await CreateTxAsync(new { accountId = account, kind = "expense", amount = 20m, occurredOn = Today.AddDays(-1), description = "Lunch" });
        await CreateTxAsync(new { accountId = account, kind = "expense", amount = 30m, occurredOn = Today, description = "Dinner" });

        var byKind = await ListTxAsync($"?accountId={account}&kind=expense");
        Assert.Equal(2, byKind.Count);
        Assert.All(byKind, t => Assert.Equal("expense", t.Kind));

        var byText = await ListTxAsync($"?accountId={account}&text=uber");
        Assert.Single(byText);
        Assert.Equal("UBER trip", byText[0].Description);

        // Stable paging: ordered by occurred_on desc then id; page 1 then page 2 are disjoint and ordered.
        var page1 = await ListTxAsync($"?accountId={account}&skip=0&take=1");
        var page2 = await ListTxAsync($"?accountId={account}&skip=1&take=1");
        Assert.Single(page1);
        Assert.Single(page2);
        Assert.NotEqual(page1[0].Id, page2[0].Id);
        Assert.Equal("Dinner", page1[0].Description);   // most recent first
    }

    [Fact]
    public async Task Archived_account_rejects_new_transactions()
    {
        await AuthAsync("tx-archived");
        var account = await CreateAccountAsync(new { name = "Wallet", type = "cash", currency = "BRL", displayOrder = 0 });
        Assert.Equal(HttpStatusCode.OK, (await _client.PostAsync($"{Accounts}/{account}/archive", null)).StatusCode);

        var rejected = await PostTxAsync(new { accountId = account, kind = "income", amount = 10m, occurredOn = Today, description = "Late" });
        Assert.Equal(HttpStatusCode.Conflict, rejected);
    }

    [Fact]
    public async Task Posted_transaction_cannot_be_edited_after_void()
    {
        await AuthAsync("tx-void-edit");
        var account = await CreateAccountAsync(new { name = "Wallet", type = "cash", currency = "BRL", displayOrder = 0 });
        var tx = await CreateTxAsync(new { accountId = account, kind = "expense", amount = 30m, occurredOn = Today, description = "X" });

        Assert.Equal(HttpStatusCode.OK, (await _client.PostAsync($"{Transactions}/{tx.Id}/void", null)).StatusCode);

        var edit = await _client.PutAsJsonAsync($"{Transactions}/{tx.Id}", new { description = "Y" });
        Assert.Equal(HttpStatusCode.Conflict, edit.StatusCode);
    }

    // ── helpers ──

    private Task AuthAsync(string username) =>
        IdentityHelper.AuthenticateAsync(_client, _factory.ConnectionString, $"{username}@example.com", username);

    private async Task<Guid> CreateAccountAsync(object body)
    {
        var response = await _client.PostAsJsonAsync(Accounts, body);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<AccountEnvelope>())!.Data.Id;
    }

    private async Task<TxNode> CreateTxAsync(object body)
    {
        var response = await _client.PostAsJsonAsync(Transactions, body);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<SingleEnvelope>())!.Data;
    }

    private async Task<HttpStatusCode> PostTxAsync(object body) =>
        (await _client.PostAsJsonAsync(Transactions, body)).StatusCode;

    private async Task<IReadOnlyList<TxNode>> ListTxAsync(string qs) =>
        (await _client.GetFromJsonAsync<ListEnvelope>($"{Transactions}{qs}"))!.Data;

    private async Task<BalanceNode> GetBalanceAsync(Guid accountId) =>
        (await _client.GetFromJsonAsync<BalanceEnvelope>($"{Accounts}/{accountId}/balance"))!.Data;

    private sealed record AccountEnvelope(AccountNode Data);
    private sealed record AccountNode(Guid Id);

    private sealed record ListEnvelope(IReadOnlyList<TxNode> Data);
    private sealed record SingleEnvelope(TxNode Data);
    private sealed record TxNode(
        Guid Id, Guid AccountId, string Kind, string Status, decimal Amount, string Currency,
        string Description, Guid? TransferGroupId, decimal? FxRate);

    private sealed record BalanceEnvelope(BalanceNode Data);
    private sealed record BalanceNode(Guid AccountId, string Currency, decimal Posted, decimal Projected);

    private sealed record AuditEnvelope(IReadOnlyList<AuditNode> Data);
    private sealed record AuditNode(string EventType);
}
