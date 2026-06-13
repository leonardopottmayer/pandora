using System.Net;
using System.Net.Http.Json;
using Pottmayer.Pandora.IntegrationTests.Support;
using Xunit;

namespace Pottmayer.Pandora.IntegrationTests.Modules.Finances;

[Collection("Integration")]
public sealed class InstallmentsTests : IAsyncLifetime
{
    private const string Cards = "/api/v1/finances/cards";
    private const string Transactions = "/api/v1/finances/transactions";
    private const string Statements = "/api/v1/finances/statements";

    private readonly PandoraWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public InstallmentsTests(PandoraWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public Task InitializeAsync() => _factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Purchase_in_3x_splits_across_three_consecutive_statements()
    {
        await AuthAsync("inst-split");
        var card = await CreateCardAsync();

        var first = await CreatePurchaseAsync(card, amount: 1000m, installments: 3, description: "Geladeira");
        Assert.NotNull(first.InstallmentPlanId);
        Assert.Equal((short)1, first.InstallmentNumber);
        Assert.Equal(333.34m, first.Amount);

        var plans = (await _client.GetFromJsonAsync<ListEnvelope<InstallmentPlanNode>>($"{Cards}/{card}/installment-plans"))!.Data;
        var plan = Assert.Single(plans);
        Assert.Equal(3, plan.InstallmentCount);
        Assert.Equal(1000m, plan.TotalAmount);
        Assert.Equal(1000m, plan.RemainingAmount);
        Assert.Equal(new[] { 333.34m, 333.33m, 333.33m }, plan.Installments.Select(i => i.Amount).ToArray());
        Assert.Equal(new[] { "2026-06", "2026-07", "2026-08" }, plan.Installments.Select(i => i.ReferenceMonth).ToArray());

        var statements = (await _client.GetFromJsonAsync<ListEnvelope<CardStatementNode>>($"{Cards}/{card}/statements"))!.Data;
        Assert.Equal(333.34m, statements.Single(s => s.ReferenceMonth == "2026-06").TotalAmount);
        Assert.Equal(333.33m, statements.Single(s => s.ReferenceMonth == "2026-07").TotalAmount);
        Assert.Equal(333.33m, statements.Single(s => s.ReferenceMonth == "2026-08").TotalAmount);
    }

    [Fact]
    public async Task Installment_on_a_closed_statement_cannot_be_voided()
    {
        await AuthAsync("inst-closed");
        var card = await CreateCardAsync();

        var first = await CreatePurchaseAsync(card, amount: 300m, installments: 3, description: "Sofa");
        Assert.Equal(HttpStatusCode.OK, (await _client.PostAsync($"{Statements}/{first.CardStatementId}/close", null)).StatusCode);

        var void1 = await _client.PostAsJsonAsync($"{Transactions}/{first.Id}/void", new { reason = "changed mind" });
        Assert.Equal(HttpStatusCode.Conflict, void1.StatusCode);
    }

    [Fact]
    public async Task Voiding_the_whole_plan_cancels_open_installments_and_clears_statement_totals()
    {
        await AuthAsync("inst-plan-void");
        var card = await CreateCardAsync();

        var first = await CreatePurchaseAsync(card, amount: 600m, installments: 3, description: "Notebook");

        var voided = await _client.PostAsJsonAsync($"{Transactions}/{first.Id}/void", new { reason = "cancel", voidEntirePlan = true });
        Assert.Equal(HttpStatusCode.OK, voided.StatusCode);

        var plan = (await _client.GetFromJsonAsync<SingleEnvelope<InstallmentPlanNode>>($"/api/v1/finances/installment-plans/{first.InstallmentPlanId}"))!.Data;
        Assert.All(plan.Installments, i => Assert.Equal("void", i.Status));
        Assert.Equal(0m, plan.RemainingAmount);

        var statements = (await _client.GetFromJsonAsync<ListEnvelope<CardStatementNode>>($"{Cards}/{card}/statements"))!.Data;
        Assert.All(statements, s => Assert.Equal(0m, s.TotalAmount));
    }

    [Fact]
    public async Task Voiding_a_single_open_installment_reduces_only_its_statement_total()
    {
        await AuthAsync("inst-single-void");
        var card = await CreateCardAsync();

        var first = await CreatePurchaseAsync(card, amount: 900m, installments: 3, description: "TV");

        var voided = await _client.PostAsJsonAsync($"{Transactions}/{first.Id}/void", new { reason = "first only" });
        Assert.Equal(HttpStatusCode.OK, voided.StatusCode);

        var statements = (await _client.GetFromJsonAsync<ListEnvelope<CardStatementNode>>($"{Cards}/{card}/statements"))!.Data;
        Assert.Equal(0m, statements.Single(s => s.ReferenceMonth == "2026-06").TotalAmount);
        Assert.Equal(300m, statements.Single(s => s.ReferenceMonth == "2026-07").TotalAmount);
        Assert.Equal(300m, statements.Single(s => s.ReferenceMonth == "2026-08").TotalAmount);
    }

    private Task AuthAsync(string username) =>
        IdentityHelper.AuthenticateAsync(_client, _factory.ConnectionString, $"{username}@example.com", username);

    private async Task<Guid> CreateCardAsync()
    {
        var response = await _client.PostAsJsonAsync(Cards, new { name = "Card", closingDay = 10, dueDay = 20, currency = "BRL", creditLimit = 5000m });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<SingleEnvelope<CardNode>>())!.Data.Id;
    }

    private async Task<TransactionNode> CreatePurchaseAsync(Guid card, decimal amount, int installments, string description)
    {
        var response = await _client.PostAsJsonAsync(Transactions, new
        {
            cardId = card,
            kind = "expense",
            amount,
            occurredOn = new DateOnly(2026, 6, 5),
            description,
            installments
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<SingleEnvelope<TransactionNode>>())!.Data;
    }

    private sealed record SingleEnvelope<T>(T Data);
    private sealed record ListEnvelope<T>(IReadOnlyList<T> Data);
    private sealed record CardNode(Guid Id);
    private sealed record TransactionNode(Guid Id, Guid? CardStatementId, Guid? InstallmentPlanId, short? InstallmentNumber, decimal Amount);
    private sealed record CardStatementNode(Guid Id, string ReferenceMonth, string Status, decimal TotalAmount);
    private sealed record InstallmentItemNode(short Number, decimal Amount, string ReferenceMonth, string Status, string StatementStatus);
    private sealed record InstallmentPlanNode(Guid Id, int InstallmentCount, decimal TotalAmount, decimal RemainingAmount, IReadOnlyList<InstallmentItemNode> Installments);
}
