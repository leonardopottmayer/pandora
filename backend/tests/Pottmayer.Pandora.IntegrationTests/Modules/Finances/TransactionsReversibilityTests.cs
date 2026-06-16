using System.Net;
using System.Net.Http.Json;
using Pottmayer.Pandora.IntegrationTests.Support;
using Xunit;

namespace Pottmayer.Pandora.IntegrationTests.Modules.Finances;

/// <summary>
/// Covers docs/modules/finances/reversibilidade-e-consistencia.md: statement amount sync on void
/// (Etapa 1), unvoid (Etapa 2), blocking deletion of cards/accounts with history (Etapa 3), and the
/// generic reversal mechanism (Etapa 4).
/// </summary>
[Collection("Integration")]
public sealed class TransactionsReversibilityTests : IAsyncLifetime
{
    private const string Accounts = "/api/v1/finances/accounts";
    private const string Cards = "/api/v1/finances/cards";
    private const string Transactions = "/api/v1/finances/transactions";
    private const string Statements = "/api/v1/finances/statements";
    private const string InstallmentPlans = "/api/v1/finances/installment-plans";

    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);

    private readonly PandoraWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public TransactionsReversibilityTests(PandoraWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public Task InitializeAsync() => _factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    // ── Etapa 1: StatementAmountSync ──

    [Fact]
    public async Task Voiding_a_standalone_purchase_reverts_the_statement_total()
    {
        await AuthAsync("rev-e1-purchase");
        var card = await CreateCardAsync(new { name = "Card", closingDay = 28, dueDay = 5, currency = "BRL" });

        var tx = await CreateCardTxAsync(new { cardId = card, kind = "expense", amount = 100m, occurredOn = Today, description = "Market" });
        Assert.Equal(100m, (await GetStatementAsync(tx.CardStatementId!.Value)).Statement.TotalAmount);

        Assert.Equal(HttpStatusCode.OK, (await _client.PostAsync($"{Transactions}/{tx.Id}/void", null)).StatusCode);

        Assert.Equal(0m, (await GetStatementAsync(tx.CardStatementId!.Value)).Statement.TotalAmount);
    }

    [Fact]
    public async Task Voiding_a_standalone_refund_reverts_the_statement_total()
    {
        await AuthAsync("rev-e1-refund");
        var card = await CreateCardAsync(new { name = "Card", closingDay = 28, dueDay = 5, currency = "BRL" });

        var purchase = await CreateCardTxAsync(new { cardId = card, kind = "expense", amount = 100m, occurredOn = Today, description = "Purchase" });
        var refund = await CreateCardTxAsync(new { cardId = card, kind = "refund", amount = 25m, occurredOn = Today, description = "Refund" });
        Assert.Equal(75m, (await GetStatementAsync(purchase.CardStatementId!.Value)).Statement.TotalAmount);

        Assert.Equal(HttpStatusCode.OK, (await _client.PostAsync($"{Transactions}/{refund.Id}/void", null)).StatusCode);

        Assert.Equal(100m, (await GetStatementAsync(purchase.CardStatementId!.Value)).Statement.TotalAmount);
    }

    [Fact]
    public async Task Voiding_a_statement_payment_reverts_the_paid_amount_and_balance()
    {
        await AuthAsync("rev-e1-payment");
        var account = await CreateAccountAsync(new { name = "Checking", type = "checking", currency = "BRL", displayOrder = 0, openingBalance = 1000m });
        var card = await CreateCardAsync(new { name = "Card", closingDay = 28, dueDay = 5, currency = "BRL", defaultPaymentAccountId = account });

        var purchase = await CreateCardTxAsync(new { cardId = card, kind = "expense", amount = 120m, occurredOn = Today, description = "Market" });
        Assert.Equal(HttpStatusCode.OK, (await _client.PostAsync($"{Statements}/{purchase.CardStatementId}/close", null)).StatusCode);

        var pay = await _client.PostAsJsonAsync($"{Statements}/{purchase.CardStatementId}/pay", new { accountId = account, amount = 120m, occurredOn = Today });
        Assert.Equal(HttpStatusCode.OK, pay.StatusCode);
        Assert.Equal("paid", (await pay.Content.ReadFromJsonAsync<SingleEnvelope<CardStatementNode>>())!.Data.Status);
        Assert.Equal(880m, (await GetBalanceAsync(account)).Posted);

        var payment = (await ListTxAsync($"?accountId={account}&kind=card-statement-payment")).Single();

        Assert.Equal(HttpStatusCode.OK, (await _client.PostAsync($"{Transactions}/{payment.Id}/void", null)).StatusCode);

        var afterVoid = (await GetStatementAsync(purchase.CardStatementId!.Value)).Statement;
        Assert.Equal(0m, afterVoid.PaidAmount);
        Assert.NotEqual("paid", afterVoid.Status);
        Assert.Equal(1000m, (await GetBalanceAsync(account)).Posted);
    }

    // ── Etapa 2: Unvoid ──

    [Fact]
    public async Task Voiding_then_unvoiding_a_standalone_purchase_restores_the_statement_total()
    {
        await AuthAsync("rev-e2-purchase");
        var card = await CreateCardAsync(new { name = "Card", closingDay = 28, dueDay = 5, currency = "BRL" });

        var tx = await CreateCardTxAsync(new { cardId = card, kind = "expense", amount = 100m, occurredOn = Today, description = "Market" });

        Assert.Equal(HttpStatusCode.OK, (await _client.PostAsync($"{Transactions}/{tx.Id}/void", null)).StatusCode);
        Assert.Equal(0m, (await GetStatementAsync(tx.CardStatementId!.Value)).Statement.TotalAmount);

        var unvoid = await _client.PostAsync($"{Transactions}/{tx.Id}/unvoid", null);
        Assert.Equal(HttpStatusCode.OK, unvoid.StatusCode);
        Assert.Equal("posted", (await unvoid.Content.ReadFromJsonAsync<SingleEnvelope<TxNode>>())!.Data.Status);

        Assert.Equal(100m, (await GetStatementAsync(tx.CardStatementId!.Value)).Statement.TotalAmount);
    }

    [Fact]
    public async Task Unvoiding_a_posted_transaction_returns_conflict()
    {
        await AuthAsync("rev-e2-notvoid");
        var card = await CreateCardAsync(new { name = "Card", closingDay = 28, dueDay = 5, currency = "BRL" });
        var tx = await CreateCardTxAsync(new { cardId = card, kind = "expense", amount = 100m, occurredOn = Today, description = "Market" });

        var unvoid = await _client.PostAsync($"{Transactions}/{tx.Id}/unvoid", null);
        Assert.Equal(HttpStatusCode.Conflict, unvoid.StatusCode);
    }

    [Fact]
    public async Task Voiding_then_unvoiding_a_statement_payment_restores_the_paid_amount_and_balance()
    {
        await AuthAsync("rev-e2-payment");
        var account = await CreateAccountAsync(new { name = "Checking", type = "checking", currency = "BRL", displayOrder = 0, openingBalance = 1000m });
        var card = await CreateCardAsync(new { name = "Card", closingDay = 28, dueDay = 5, currency = "BRL", defaultPaymentAccountId = account });

        var purchase = await CreateCardTxAsync(new { cardId = card, kind = "expense", amount = 120m, occurredOn = Today, description = "Market" });
        Assert.Equal(HttpStatusCode.OK, (await _client.PostAsync($"{Statements}/{purchase.CardStatementId}/close", null)).StatusCode);
        var pay = await _client.PostAsJsonAsync($"{Statements}/{purchase.CardStatementId}/pay", new { accountId = account, amount = 120m, occurredOn = Today });
        Assert.Equal(HttpStatusCode.OK, pay.StatusCode);

        var payment = (await ListTxAsync($"?accountId={account}&kind=card-statement-payment")).Single();
        Assert.Equal(HttpStatusCode.OK, (await _client.PostAsync($"{Transactions}/{payment.Id}/void", null)).StatusCode);

        var unvoid = await _client.PostAsync($"{Transactions}/{payment.Id}/unvoid", null);
        Assert.Equal(HttpStatusCode.OK, unvoid.StatusCode);

        var afterUnvoid = (await GetStatementAsync(purchase.CardStatementId!.Value)).Statement;
        Assert.Equal(120m, afterUnvoid.PaidAmount);
        Assert.Equal("paid", afterUnvoid.Status);
        Assert.Equal(880m, (await GetBalanceAsync(account)).Posted);
    }

    [Fact]
    public async Task Voiding_then_unvoiding_a_single_installment_restores_its_statement_total()
    {
        await AuthAsync("rev-e2-single-inst");
        var card = await CreateCardAsync(new { name = "Card", closingDay = 28, dueDay = 5, currency = "BRL", creditLimit = 5000m });

        var first = await CreatePurchaseAsync(card, amount: 900m, installments: 3, description: "TV");
        Assert.Equal(900m, await TotalAcrossStatementsAsync(card));

        Assert.Equal(HttpStatusCode.OK, (await _client.PostAsync($"{Transactions}/{first.Id}/void", null)).StatusCode);
        Assert.Equal(0m, (await GetStatementAsync(first.CardStatementId!.Value)).Statement.TotalAmount);
        Assert.Equal(600m, await TotalAcrossStatementsAsync(card));

        var unvoid = await _client.PostAsync($"{Transactions}/{first.Id}/unvoid", null);
        Assert.Equal(HttpStatusCode.OK, unvoid.StatusCode);

        Assert.Equal(300m, (await GetStatementAsync(first.CardStatementId!.Value)).Statement.TotalAmount);
        Assert.Equal(900m, await TotalAcrossStatementsAsync(card));
    }

    [Fact]
    public async Task Voiding_then_unvoiding_the_entire_plan_restores_all_installments()
    {
        await AuthAsync("rev-e2-plan");
        var card = await CreateCardAsync(new { name = "Card", closingDay = 28, dueDay = 5, currency = "BRL", creditLimit = 5000m });

        var first = await CreatePurchaseAsync(card, amount: 600m, installments: 3, description: "Notebook");

        var voided = await _client.PostAsJsonAsync($"{Transactions}/{first.Id}/void", new { reason = "cancel", voidEntirePlan = true });
        Assert.Equal(HttpStatusCode.OK, voided.StatusCode);

        var planAfterVoid = (await _client.GetFromJsonAsync<SingleEnvelope<InstallmentPlanNode>>($"{InstallmentPlans}/{first.InstallmentPlanId}"))!.Data;
        Assert.All(planAfterVoid.Installments, i => Assert.Equal("void", i.Status));
        Assert.Equal(0m, planAfterVoid.RemainingAmount);
        Assert.Equal(0m, await TotalAcrossStatementsAsync(card));

        var unvoided = await _client.PostAsJsonAsync($"{Transactions}/{first.Id}/unvoid", new { unvoidEntirePlan = true });
        Assert.Equal(HttpStatusCode.OK, unvoided.StatusCode);

        var planAfterUnvoid = (await _client.GetFromJsonAsync<SingleEnvelope<InstallmentPlanNode>>($"{InstallmentPlans}/{first.InstallmentPlanId}"))!.Data;
        Assert.All(planAfterUnvoid.Installments, i => Assert.Equal("posted", i.Status));
        Assert.Equal(600m, planAfterUnvoid.RemainingAmount);
        Assert.Equal(600m, await TotalAcrossStatementsAsync(card));
    }

    // ── Etapa 3: block delete with history ──

    [Fact]
    public async Task Deleting_an_account_with_transaction_history_returns_conflict()
    {
        await AuthAsync("rev-e3-account");
        var account = await CreateAccountAsync(new { name = "Wallet", type = "cash", currency = "BRL", displayOrder = 0 });
        await CreateTxAsync(new { accountId = account, kind = "income", amount = 10m, occurredOn = Today, description = "Gift" });

        var delete = await _client.DeleteAsync($"{Accounts}/{account}");
        Assert.Equal(HttpStatusCode.Conflict, delete.StatusCode);
    }

    [Fact]
    public async Task Deleting_an_account_without_history_succeeds()
    {
        await AuthAsync("rev-e3-account-clean");
        var account = await CreateAccountAsync(new { name = "Wallet", type = "cash", currency = "BRL", displayOrder = 0 });

        var delete = await _client.DeleteAsync($"{Accounts}/{account}");
        Assert.Equal(HttpStatusCode.OK, delete.StatusCode);
    }

    [Fact]
    public async Task Deleting_a_card_with_statement_history_returns_conflict()
    {
        await AuthAsync("rev-e3-card");
        var card = await CreateCardAsync(new { name = "Card", closingDay = 28, dueDay = 5, currency = "BRL" });
        await CreateCardTxAsync(new { cardId = card, kind = "expense", amount = 10m, occurredOn = Today, description = "Market" });

        var delete = await _client.DeleteAsync($"{Cards}/{card}");
        Assert.Equal(HttpStatusCode.Conflict, delete.StatusCode);
    }

    [Fact]
    public async Task Deleting_a_card_without_history_succeeds()
    {
        await AuthAsync("rev-e3-card-clean");
        var card = await CreateCardAsync(new { name = "Card", closingDay = 28, dueDay = 5, currency = "BRL" });

        var delete = await _client.DeleteAsync($"{Cards}/{card}");
        Assert.Equal(HttpStatusCode.OK, delete.StatusCode);
    }

    // ── Etapa 4: generic reversal ──

    [Fact]
    public async Task Reversing_an_account_expense_creates_a_linked_income_dated_today_and_restores_the_balance()
    {
        await AuthAsync("rev-e4-account");
        var account = await CreateAccountAsync(new { name = "Wallet", type = "cash", currency = "BRL", displayOrder = 0, openingBalance = 1000m });
        var expense = await CreateTxAsync(new { accountId = account, kind = "expense", amount = 200m, occurredOn = Today.AddDays(-5), description = "Trip" });
        Assert.Equal(800m, (await GetBalanceAsync(account)).Posted);

        var response = await _client.PostAsync($"{Transactions}/{expense.Id}/reverse", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var reversal = (await response.Content.ReadFromJsonAsync<SingleEnvelope<TxNode>>())!.Data;

        Assert.Equal("income", reversal.Kind);
        Assert.Equal(200m, reversal.Amount);
        Assert.Equal(Today, reversal.OccurredOn);
        Assert.Equal("reversal", reversal.Origin);
        Assert.Equal(expense.Id, reversal.ReversedTransactionId);

        Assert.Equal(1000m, (await GetBalanceAsync(account)).Posted);
    }

    [Fact]
    public async Task Reversing_a_voided_transaction_returns_conflict()
    {
        await AuthAsync("rev-e4-notposted");
        var account = await CreateAccountAsync(new { name = "Wallet", type = "cash", currency = "BRL", displayOrder = 0 });
        var expense = await CreateTxAsync(new { accountId = account, kind = "expense", amount = 100m, occurredOn = Today, description = "X" });
        Assert.Equal(HttpStatusCode.OK, (await _client.PostAsync($"{Transactions}/{expense.Id}/void", null)).StatusCode);

        var reverse = await _client.PostAsync($"{Transactions}/{expense.Id}/reverse", null);
        Assert.Equal(HttpStatusCode.Conflict, reverse.StatusCode);
    }

    [Fact]
    public async Task Reversing_an_already_reversed_transaction_returns_conflict()
    {
        await AuthAsync("rev-e4-alreadyreversed");
        var account = await CreateAccountAsync(new { name = "Wallet", type = "cash", currency = "BRL", displayOrder = 0 });
        var expense = await CreateTxAsync(new { accountId = account, kind = "expense", amount = 100m, occurredOn = Today, description = "X" });

        Assert.Equal(HttpStatusCode.OK, (await _client.PostAsync($"{Transactions}/{expense.Id}/reverse", null)).StatusCode);

        var second = await _client.PostAsync($"{Transactions}/{expense.Id}/reverse", null);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Reversing_an_installment_returns_reversal_not_supported()
    {
        await AuthAsync("rev-e4-installment");
        var card = await CreateCardAsync(new { name = "Card", closingDay = 28, dueDay = 5, currency = "BRL", creditLimit = 5000m });
        var first = await CreatePurchaseAsync(card, amount: 300m, installments: 3, description: "Sofa");

        var reverse = await _client.PostAsync($"{Transactions}/{first.Id}/reverse", null);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, reverse.StatusCode);
    }

    [Fact]
    public async Task Reversing_a_transfer_leg_creates_an_opposite_pair_and_restores_balances()
    {
        await AuthAsync("rev-e4-transfer");
        var a = await CreateAccountAsync(new { name = "A", type = "checking", currency = "BRL", displayOrder = 0, openingBalance = 500m });
        var b = await CreateAccountAsync(new { name = "B", type = "savings", currency = "BRL", displayOrder = 1 });

        var transfer = await _client.PostAsJsonAsync($"{Transactions}/transfer", new
        {
            fromAccountId = a,
            toAccountId = b,
            amountOut = 200m,
            occurredOn = Today,
            description = "Move"
        });
        Assert.Equal(HttpStatusCode.OK, transfer.StatusCode);
        var legs = (await transfer.Content.ReadFromJsonAsync<ListEnvelope<TxNode>>())!.Data;
        var outLeg = legs.Single(l => l.Kind == "transfer-out");

        Assert.Equal(300m, (await GetBalanceAsync(a)).Posted);
        Assert.Equal(200m, (await GetBalanceAsync(b)).Posted);

        var response = await _client.PostAsync($"{Transactions}/{outLeg.Id}/reverse", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var reversal = (await response.Content.ReadFromJsonAsync<SingleEnvelope<TxNode>>())!.Data;

        Assert.Equal("transfer-in", reversal.Kind);
        Assert.Equal(a, reversal.AccountId);
        Assert.Equal(200m, reversal.Amount);
        Assert.Equal(outLeg.Id, reversal.ReversedTransactionId);

        Assert.Equal(500m, (await GetBalanceAsync(a)).Posted);
        Assert.Equal(0m, (await GetBalanceAsync(b)).Posted);
    }

    [Fact]
    public async Task Reversing_a_statement_payment_creates_a_refund_and_reduces_the_paid_amount()
    {
        await AuthAsync("rev-e4-payment");
        var account = await CreateAccountAsync(new { name = "Checking", type = "checking", currency = "BRL", displayOrder = 0, openingBalance = 1000m });
        var card = await CreateCardAsync(new { name = "Card", closingDay = 28, dueDay = 5, currency = "BRL", defaultPaymentAccountId = account });

        var purchase = await CreateCardTxAsync(new { cardId = card, kind = "expense", amount = 120m, occurredOn = Today, description = "Market" });
        Assert.Equal(HttpStatusCode.OK, (await _client.PostAsync($"{Statements}/{purchase.CardStatementId}/close", null)).StatusCode);
        var pay = await _client.PostAsJsonAsync($"{Statements}/{purchase.CardStatementId}/pay", new { accountId = account, amount = 120m, occurredOn = Today });
        Assert.Equal(HttpStatusCode.OK, pay.StatusCode);
        Assert.Equal(880m, (await GetBalanceAsync(account)).Posted);

        var payment = (await ListTxAsync($"?accountId={account}&kind=card-statement-payment")).Single();

        var response = await _client.PostAsync($"{Transactions}/{payment.Id}/reverse", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var reversal = (await response.Content.ReadFromJsonAsync<SingleEnvelope<TxNode>>())!.Data;

        Assert.Equal("refund", reversal.Kind);
        Assert.Equal(account, reversal.AccountId);
        Assert.Equal(120m, reversal.Amount);
        Assert.Equal(payment.Id, reversal.ReversedTransactionId);

        Assert.Equal(1000m, (await GetBalanceAsync(account)).Posted);
        Assert.Equal(0m, (await GetStatementAsync(purchase.CardStatementId!.Value)).Statement.PaidAmount);
    }

    [Fact]
    public async Task Reversing_a_standalone_card_purchase_lands_on_the_current_open_statement()
    {
        await AuthAsync("rev-e4-cardpurchase");
        var card = await CreateCardAsync(new { name = "Card", closingDay = 28, dueDay = 5, currency = "BRL" });

        var original = await CreateCardTxAsync(new { cardId = card, kind = "expense", amount = 80m, occurredOn = Today.AddMonths(-2), description = "Old purchase" });
        var current = await CreateCardTxAsync(new { cardId = card, kind = "expense", amount = 1m, occurredOn = Today, description = "Today purchase" });
        Assert.NotEqual(original.CardStatementId, current.CardStatementId);

        var response = await _client.PostAsync($"{Transactions}/{original.Id}/reverse", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var reversal = (await response.Content.ReadFromJsonAsync<SingleEnvelope<TxNode>>())!.Data;

        Assert.Equal("refund", reversal.Kind);
        Assert.Equal(original.Id, reversal.ReversedTransactionId);
        Assert.Equal(current.CardStatementId, reversal.CardStatementId);
        Assert.NotEqual(original.CardStatementId, reversal.CardStatementId);

        Assert.Equal(-79m, (await GetStatementAsync(current.CardStatementId!.Value)).Statement.TotalAmount);
        Assert.Equal(80m, (await GetStatementAsync(original.CardStatementId!.Value)).Statement.TotalAmount);
    }

    // ── Installment + Statement additional scenarios ──

    [Fact]
    public async Task Voiding_a_single_installment_in_open_statement_reduces_statement_total()
    {
        await AuthAsync("rev-inst-void1");
        var card = await CreateCardAsync(new { name = "Card", closingDay = 28, dueDay = 5, currency = "BRL", creditLimit = 5000m });
        var first = await CreatePurchaseAsync(card, amount: 90m, installments: 3, description: "Laptop");

        var stmt = await GetStatementAsync(first.CardStatementId!.Value);
        Assert.True(stmt.Statement.TotalAmount > 0);

        Assert.Equal(HttpStatusCode.OK, (await _client.PostAsync($"{Transactions}/{first.Id}/void", null)).StatusCode);

        var after = await GetStatementAsync(first.CardStatementId!.Value);
        Assert.Equal(0m, after.Statement.TotalAmount);
    }

    [Fact]
    public async Task Voiding_entire_installment_plan_zeroes_all_open_statement_totals()
    {
        await AuthAsync("rev-inst-void-plan");
        var card = await CreateCardAsync(new { name = "Card", closingDay = 28, dueDay = 5, currency = "BRL", creditLimit = 5000m });
        var first = await CreatePurchaseAsync(card, amount: 90m, installments: 3, description: "Sofa");

        Assert.Equal(HttpStatusCode.OK,
            (await _client.PostAsJsonAsync($"{Transactions}/{first.Id}/void", new { voidEntirePlan = true })).StatusCode);

        Assert.Equal(0m, await TotalAcrossStatementsAsync(card));
    }

    [Fact]
    public async Task Voiding_single_installment_in_closed_statement_returns_conflict()
    {
        await AuthAsync("rev-inst-closed");
        var card = await CreateCardAsync(new { name = "Card", closingDay = 28, dueDay = 5, currency = "BRL", creditLimit = 5000m });
        // Create a purchase with a purchase date 2 months ago so its statement is a different one from today's.
        var first = await CreateCardTxAsync(new
        {
            cardId = card, kind = "expense", amount = 30m,
            occurredOn = Today.AddMonths(-2), description = "Old", installments = 2
        });
        Assert.Equal(HttpStatusCode.OK, (await _client.PostAsync($"{Statements}/{first.CardStatementId}/close", null)).StatusCode);

        var response = await _client.PostAsync($"{Transactions}/{first.Id}/void", null);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Voiding_reversed_transaction_succeeds()
    {
        await AuthAsync("rev-void-reversed");
        var account = await CreateAccountAsync(new { name = "Checking", type = "checking", currency = "BRL", displayOrder = 0, openingBalance = 500m });
        var tx = (await _client.PostAsJsonAsync(Transactions, new
        {
            accountId = account, kind = "expense", amount = 100m, occurredOn = Today, description = "Market"
        })).EnsureSuccessStatusCode();
        var txNode = (await tx.Content.ReadFromJsonAsync<SingleEnvelope<TxNode>>())!.Data;

        // Reverse → creates a linked income
        Assert.Equal(HttpStatusCode.OK, (await _client.PostAsync($"{Transactions}/{txNode.Id}/reverse", null)).StatusCode);

        // Voiding the original (already reversed) should still work
        Assert.Equal(HttpStatusCode.OK, (await _client.PostAsync($"{Transactions}/{txNode.Id}/void", null)).StatusCode);
    }

    [Fact]
    public async Task Paying_statement_with_installment_increments_paid_installments_count()
    {
        await AuthAsync("rev-paid-installments");
        var account = await CreateAccountAsync(new { name = "Checking", type = "checking", currency = "BRL", displayOrder = 0, openingBalance = 1000m });
        var card = await CreateCardAsync(new { name = "Card", closingDay = 28, dueDay = 5, currency = "BRL", defaultPaymentAccountId = account });

        var first = await CreatePurchaseAsync(card, amount: 90m, installments: 3, description: "Tablet");

        // Close and fully pay the statement that holds installment 1
        Assert.Equal(HttpStatusCode.OK, (await _client.PostAsync($"{Statements}/{first.CardStatementId}/close", null)).StatusCode);
        var payResult = await _client.PostAsJsonAsync($"{Statements}/{first.CardStatementId}/pay", new
        {
            accountId = account,
            amount = first.Amount,
            occurredOn = Today
        });
        Assert.Equal(HttpStatusCode.OK, payResult.StatusCode);

        var plan = await _client.GetFromJsonAsync<SingleEnvelope<InstallmentPlanNode>>(
            $"{InstallmentPlans}/{first.InstallmentPlanId}");
        Assert.Equal(1, plan!.Data.PaidInstallments);
        Assert.Equal(3, plan.Data.InstallmentCount);
    }

    // ── helpers ──

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

    private async Task<TxNode> CreateTxAsync(object body)
    {
        var response = await _client.PostAsJsonAsync(Transactions, body);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<SingleEnvelope<TxNode>>())!.Data;
    }

    private Task<TxNode> CreateCardTxAsync(object body) => CreateTxAsync(body);

    private async Task<TxNode> CreatePurchaseAsync(Guid card, decimal amount, int installments, string description)
    {
        return await CreateCardTxAsync(new
        {
            cardId = card,
            kind = "expense",
            amount,
            occurredOn = Today,
            description,
            installments
        });
    }

    private async Task<IReadOnlyList<TxNode>> ListTxAsync(string qs) =>
        (await _client.GetFromJsonAsync<ListEnvelope<TxNode>>($"{Transactions}{qs}"))!.Data;

    private async Task<BalanceNode> GetBalanceAsync(Guid accountId) =>
        (await _client.GetFromJsonAsync<SingleEnvelope<BalanceNode>>($"{Accounts}/{accountId}/balance"))!.Data;

    private async Task<StatementDetailNode> GetStatementAsync(Guid statementId) =>
        (await _client.GetFromJsonAsync<SingleEnvelope<StatementDetailNode>>($"{Statements}/{statementId}"))!.Data;

    private async Task<decimal> TotalAcrossStatementsAsync(Guid card)
    {
        var statements = (await _client.GetFromJsonAsync<ListEnvelope<CardStatementNode>>($"{Cards}/{card}/statements"))!.Data;
        return statements.Sum(s => s.TotalAmount);
    }

    private sealed record SingleEnvelope<T>(T Data);
    private sealed record ListEnvelope<T>(IReadOnlyList<T> Data);
    private sealed record AccountNode(Guid Id);
    private sealed record CardNode(Guid Id);

    private sealed record TxNode(
        Guid Id, Guid? AccountId, Guid? CardId, Guid? CardStatementId, Guid? PaidStatementId,
        string Kind, string Status, decimal Amount, string Currency, DateOnly OccurredOn, string Description,
        string Origin, Guid? ReversedTransactionId, Guid? TransferGroupId,
        Guid? InstallmentPlanId, short? InstallmentNumber);

    private sealed record BalanceNode(Guid AccountId, string Currency, decimal Posted, decimal Projected);

    private sealed record CardStatementNode(Guid Id, string ReferenceMonth, string Status, decimal TotalAmount, decimal PaidAmount);
    private sealed record StatementDetailNode(CardStatementNode Statement, IReadOnlyList<TxNode> Transactions);

    private sealed record InstallmentItemNode(short Number, decimal Amount, string ReferenceMonth, string Status, string StatementStatus);
    private sealed record InstallmentPlanNode(Guid Id, int InstallmentCount, int PaidInstallments, decimal TotalAmount, decimal RemainingAmount, IReadOnlyList<InstallmentItemNode> Installments);
}
