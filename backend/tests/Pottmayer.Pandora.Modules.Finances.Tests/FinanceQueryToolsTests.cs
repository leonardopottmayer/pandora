using System.Text.Json;
using Pottmayer.Pandora.Modules.Assistant.Abstractions.Commands;
using Pottmayer.Pandora.Modules.Finances.Application.Assistant;
using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Pandora.Modules.Finances.Application.Queries.GetAccountBalance;
using Pottmayer.Pandora.Modules.Finances.Application.Queries.GetAccounts;
using Pottmayer.Pandora.Modules.Finances.Application.Queries.GetCardAvailableLimit;
using Pottmayer.Pandora.Modules.Finances.Application.Queries.GetCards;
using Pottmayer.Pandora.Modules.Finances.Application.Queries.GetCardStatements;
using Pottmayer.Pandora.Modules.Finances.Application.Queries.GetPendingTransactions;
using Pottmayer.Pandora.Modules.Finances.Application.Queries.GetSystemCategories;
using Pottmayer.Pandora.Modules.Finances.Application.Queries.GetTransactions;
using Pottmayer.Pandora.Modules.Finances.Application.Queries.GetUserCategories;
using Pottmayer.Pandora.Modules.Finances.Tests.Fakes;
using Pottmayer.Tars.Core.Mediator.Abstractions;
using Pottmayer.Tars.Core.Mediator.Abstractions.Messaging;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Xunit;

namespace Pottmayer.Pandora.Modules.Finances.Tests;

/// <summary>The read-only Finances assistant tools: transactions, balances, cards and the inbox.</summary>
public sealed class FinanceQueryToolsTests
{
    private static readonly TimeZoneInfo SaoPaulo = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
    private static readonly AssistantToolContext Context = new(Guid.NewGuid(), "pt-BR", SaoPaulo);
    private static readonly FixedTimeProvider Time = new(new DateTimeOffset(2026, 10, 1, 15, 0, 0, TimeSpan.Zero));

    private static readonly AccountDto Itau = new(Guid.NewGuid(), "Itaú", "checking", "BRL", null, null, null, null, 0, null);
    private static readonly AccountDto NubankAccount = new(Guid.NewGuid(), "Nubank", "checking", "BRL", null, null, null, null, 1, null);
    // Named like the account, as in real life — "Nubank" must find the card when asked for a card.
    private static readonly CardDto Nubank = new(Guid.NewGuid(), "Nubank", null, null, 5000m, 1, 10, "BRL", NubankAccount.Id, null);

    private static readonly SystemCategoryDto Supermarket = Category("Supermercado");
    private static readonly SystemCategoryDto Food = Category("Alimentação", Supermarket);
    private static readonly SystemCategoryDto Transport = Category("Transporte");

    private static SystemCategoryDto Category(string name, params SystemCategoryDto[] children) =>
        new(Guid.NewGuid(), name.ToLowerInvariant(), name, "expense", null, null, 0, false, true, children);

    private static TransactionDto Tx(string description, decimal amount, string day, Guid? category = null,
        bool onCard = false, string kind = "expense", string status = "posted", AccountDto? account = null) =>
        new(Guid.NewGuid(), onCard ? null : (account ?? Itau).Id, null, onCard ? Nubank.Id : null, null, kind, status, amount, "BRL",
            DateOnly.Parse(day), description, null, null, category, null, null, null, null, null, "manual",
            null, null, null, null, null, null, null, null);

    /// <summary>
    /// Answers the Finances queries from fixed data, filtering transactions by period, kind and account the
    /// way the repository does, and records the transaction queries it got.
    /// </summary>
    private sealed class FinanceSender(params TransactionDto[] transactions) : ISender
    {
        public List<GetTransactionsInput> Searched { get; } = [];
        public IReadOnlyList<CardStatementDto> Statements { get; init; } = [];
        public IReadOnlyList<PendingTransactionDto> Pending { get; init; } = [];

        public ValueTask<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            object response = request switch
            {
                GetAccountsQuery => Result<IReadOnlyList<AccountDto>>.Success([Itau, NubankAccount]),
                GetCardsQuery => Result<IReadOnlyList<CardDto>>.Success([Nubank]),
                GetSystemCategoriesQuery => Result<IReadOnlyList<SystemCategoryDto>>.Success([Food, Transport]),
                GetUserCategoriesQuery => Result<IReadOnlyList<UserCategoryDto>>.Success([]),
                GetTransactionsQuery q => Search(q.Input),
                GetAccountBalanceQuery => Result<AccountBalanceDto>.Success(new AccountBalanceDto(Itau.Id, "BRL", 1234.56m, 1000m)),
                GetCardStatementsQuery => Result<IReadOnlyList<CardStatementDto>>.Success(Statements),
                GetCardAvailableLimitQuery => Result<CardAvailableLimitDto>.Success(new CardAvailableLimitDto(Nubank.Id, 5000m, 3800m)),
                GetPendingTransactionsQuery => Result<IReadOnlyList<PendingTransactionDto>>.Success(Pending),
                _ => throw new NotSupportedException(request.GetType().Name),
            };
            return ValueTask.FromResult((TResponse)response);
        }

        private Result<IReadOnlyList<TransactionDto>> Search(GetTransactionsInput input)
        {
            Searched.Add(input);
            return Result<IReadOnlyList<TransactionDto>>.Success([.. transactions
                .Where(t => (input.From is null || t.OccurredOn >= input.From) && (input.To is null || t.OccurredOn <= input.To))
                .Where(t => input.Kind is null || t.Kind == input.Kind)
                .Where(t => input.AccountId is null || t.AccountId == input.AccountId)
                .Skip(input.Skip).Take(input.Take)]);
        }
    }

    private static JsonElement Args(string json) => JsonDocument.Parse(json).RootElement.Clone();

    [Fact]
    public async Task Summarize_totals_a_month_by_category_against_the_whole_month_before()
    {
        var sender = new FinanceSender(
            Tx("Mercado", 300m, "2026-09-03", Supermarket.Id),
            Tx("Feira", 100m, "2026-09-10", Food.Id),
            Tx("Uber", 50m, "2026-09-12", Transport.Id),
            Tx("Estornado", 999m, "2026-09-13", Transport.Id, status: "void"),
            Tx("Agendado", 777m, "2026-09-29", Transport.Id, status: "pending"),
            Tx("Salário", 5000m, "2026-09-05", kind: "income"),
            Tx("Mercado", 200m, "2026-08-31", Supermarket.Id));

        var outcome = await new SummarizeTransactionsTool(sender, Time).ExecuteAsync(Context, Args(
            """{ "from": "2026-09-01", "to": "2026-09-30", "groupBy": "category", "comparePrevious": true }"""));

        Assert.Equal(
            """
            Gastos de 01/09/2026 a 30/09/2026: R$ 450,00 (3 lançamento(s))
            Antes de 01/08/2026 a 31/08/2026: R$ 200,00 (+125%)

            Por categoria:
            • Supermercado — R$ 300,00 (+50%)
            • Alimentação — R$ 100,00 (antes: nada)
            • Transporte — R$ 50,00 (antes: nada)
            """.ReplaceLineEndings("\n"),
            outcome.Message);
        Assert.DoesNotContain("450", outcome.Recap);
    }

    [Fact]
    public async Task Summarize_a_parent_category_counts_its_children()
    {
        var sender = new FinanceSender(
            Tx("Mercado", 300m, "2026-09-03", Supermarket.Id),
            Tx("Feira", 100m, "2026-09-10", Food.Id),
            Tx("Uber", 50m, "2026-09-12", Transport.Id));

        var outcome = await new SummarizeTransactionsTool(sender, Time).ExecuteAsync(Context, Args(
            """{ "from": "2026-09-01", "to": "2026-09-30", "category": "alimentacao" }"""));

        Assert.StartsWith("Gastos de 01/09/2026 a 30/09/2026: R$ 400,00 (2 lançamento(s))", outcome.Message);
    }

    [Fact]
    public async Task Summarize_without_a_period_covers_this_month_so_far()
    {
        var sender = new FinanceSender();

        await new SummarizeTransactionsTool(sender, Time).ExecuteAsync(Context, Args("{}"));

        var input = Assert.Single(sender.Searched);
        Assert.Equal((new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 1), "expense"), (input.From, input.To, input.Kind));
    }

    [Fact]
    public async Task List_shows_the_largest_card_expenses_with_the_total_of_all_that_matched()
    {
        var sender = new FinanceSender(
            Tx("iFood", 80m, "2026-09-02", onCard: true),
            Tx("iFood", 120m, "2026-09-09", onCard: true),
            Tx("iFood conta", 60m, "2026-09-09"),
            Tx("Uber", 30m, "2026-09-10", onCard: true));

        var outcome = await new ListTransactionsTool(sender).ExecuteAsync(Context, Args(
            """{ "card": "nubank", "kind": "expense", "order": "largest", "limit": 2 }"""));

        Assert.Equal(
            """
            Lançamentos:
            • 09/09 · iFood · R$ 120,00 · Nubank
            • 02/09 · iFood · R$ 80,00 · Nubank

            Mostrando 2 de 3. Total: R$ 230,00.
            """.ReplaceLineEndings("\n"),
            outcome.Message);
        Assert.Null(outcome.Listed); // nothing in Finances is changed from the chat, so nothing to point at
    }

    [Fact]
    public async Task List_refuses_an_account_it_cannot_find()
    {
        var outcome = await new ListTransactionsTool(new FinanceSender()).ExecuteAsync(Context, Args("""{ "account": "bradesco" }"""));

        Assert.False(outcome.Success);
        Assert.Equal("Não encontrei \"bradesco\" entre suas contas.", outcome.Message);
    }

    [Fact]
    public async Task A_card_and_an_account_with_the_same_name_are_told_apart()
    {
        var sender = new FinanceSender(
            Tx("iFood", 80m, "2026-09-02", onCard: true),
            Tx("Café", 12m, "2026-09-03", account: NubankAccount));
        var tool = new SummarizeTransactionsTool(sender, Time);

        var card = await tool.ExecuteAsync(Context, Args("""{ "from": "2026-09-01", "to": "2026-09-30", "card": "Nubank" }"""));
        var account = await tool.ExecuteAsync(Context, Args("""{ "from": "2026-09-01", "to": "2026-09-30", "account": "Nubank" }"""));

        Assert.StartsWith("Gastos de 01/09/2026 a 30/09/2026: R$ 80,00 (1 lançamento(s))", card.Message);
        Assert.StartsWith("Gastos de 01/09/2026 a 30/09/2026: R$ 12,00 (1 lançamento(s))", account.Message);
    }

    [Fact]
    public async Task Balances_show_each_account_with_its_projection_and_the_total()
    {
        var outcome = await new AccountBalancesTool(new FinanceSender()).ExecuteAsync(Context, Args("{}"));

        Assert.Equal(
            "Saldos:\n• Itaú — R$ 1.234,56 (previsto R$ 1.000,00)\n• Nubank — R$ 1.234,56 (previsto R$ 1.000,00)\n\nTotal: R$ 2.469,12",
            outcome.Message);
    }

    [Fact]
    public async Task Cards_show_the_unpaid_statement_the_current_one_and_the_limit_left()
    {
        CardStatementDto Statement(string status, string closes, string due, decimal total, decimal remaining) =>
            new(Guid.NewGuid(), Nubank.Id, "2026-09", DateOnly.Parse(closes), DateOnly.Parse(due), status, total, total - remaining, remaining,
                null, null, null);
        var sender = new FinanceSender
        {
            Statements =
            [
                Statement("paid", "2026-08-01", "2026-08-10", 900m, 0m),
                Statement("closed", "2026-10-01", "2026-10-10", 2340m, 2340m),
                Statement("open", "2026-11-01", "2026-11-10", 410m, 410m),
            ],
        };

        var outcome = await new ListCardsTool(sender).ExecuteAsync(Context, Args("{}"));

        Assert.Equal(
            """
            Cartões:

            Nubank
            • Fatura fechada, vence 10/10/2026: R$ 2.340,00 a pagar
            • Fatura atual: R$ 410,00 (fecha 01/11, vence 10/11)
            • Limite disponível: R$ 3.800,00
            """.ReplaceLineEndings("\n"),
            outcome.Message);
    }

    [Fact]
    public async Task Inbox_lists_what_waits_for_review_and_says_where_to_decide()
    {
        var sender = new FinanceSender
        {
            Pending =
            [
                new PendingTransactionDto(Guid.NewGuid(), "manual", null, null, Nubank.Id, "expense", 45.9m, "BRL",
                    new DateOnly(2026, 9, 4), "Mercado", null, null, null, null, null, "{}", "pending", null, null, null,
                    null, null, null, null, DateTimeOffset.UtcNow, null),
            ],
        };

        var outcome = await new ListInboxTool(sender).ExecuteAsync(Context, Args("{}"));

        Assert.Equal(
            "Caixa de entrada do Finances (1):\n• 04/09 · Mercado · R$ 45,90 · Nubank\n\nAprove ou rejeite no Finances.",
            outcome.Message);
        Assert.DoesNotContain("Mercado", outcome.Recap);
    }
}
