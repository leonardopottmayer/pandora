using System.Text.Json;
using Pottmayer.Pandora.Modules.Assistant.Abstractions.Commands;
using Pottmayer.Pandora.Modules.Finances.Application.Assistant;
using Pottmayer.Pandora.Modules.Finances.Application.Commands.CreatePendingExpense;
using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Pandora.Modules.Finances.Tests.Fakes;
using Pottmayer.Tars.Core.Mediator.Abstractions;
using Pottmayer.Tars.Core.Mediator.Abstractions.Messaging;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Xunit;

namespace Pottmayer.Pandora.Modules.Finances.Tests;

public sealed class RecordExpenseToolTests
{
    private static readonly TimeZoneInfo SaoPaulo = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
    private static readonly AssistantToolContext Context = new(Guid.NewGuid(), "pt-BR", SaoPaulo);

    // 01:00 UTC on the 5th is still the 4th in São Paulo.
    private static readonly FixedTimeProvider Time = new(new DateTimeOffset(2026, 9, 5, 1, 0, 0, TimeSpan.Zero));

    private static readonly AccountDto Itau = new(Guid.NewGuid(), "Itaú", "checking", "BRL", null, null, null, null, 0, null);
    private static readonly CardDto Nubank = new(Guid.NewGuid(), "Nubank", null, null, null, 1, 10, "BRL", Itau.Id, null);

    /// <summary>Answers each request with the scripted response of its type; echoes the pending expense it is asked to create.</summary>
    private sealed class ScriptedSender(IReadOnlyList<AccountDto> accounts, IReadOnlyList<CardDto> cards) : ISender
    {
        public List<CreatePendingExpenseInput> Created { get; } = [];

        public ValueTask<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            object response = request switch
            {
                CreatePendingExpenseCommand c => Echo(c.Input),
                _ when typeof(TResponse) == typeof(Result<IReadOnlyList<AccountDto>>) => Result<IReadOnlyList<AccountDto>>.Success(accounts),
                _ => Result<IReadOnlyList<CardDto>>.Success(cards),
            };
            return ValueTask.FromResult((TResponse)response);
        }

        private Result<PendingTransactionDto> Echo(CreatePendingExpenseInput input)
        {
            Created.Add(input);
            return Result<PendingTransactionDto>.Success(new PendingTransactionDto(
                Guid.NewGuid(), "manual", null, input.AccountId, input.CardId, "expense", input.Amount, "BRL",
                input.OccurredOn, input.Description, null, null, null, null, null, "{}", "pending", null, null, null,
                null, null, null, null, DateTimeOffset.UtcNow, null));
        }
    }

    private static JsonElement Args(string json) => JsonDocument.Parse(json).RootElement.Clone();

    [Fact]
    public async Task Sends_a_card_expense_to_the_inbox_dated_today_in_the_users_zone()
    {
        var sender = new ScriptedSender([Itau], [Nubank]);

        var outcome = await new RecordExpenseTool(sender, Time).ExecuteAsync(
            Context, Args("""{ "amount": "45,90", "description": "Mercado", "card": "nubank" }"""));

        var input = Assert.Single(sender.Created);
        Assert.Equal(Nubank.Id, input.CardId);
        Assert.Null(input.AccountId);
        Assert.Equal(45.90m, input.Amount);
        Assert.Equal(new DateOnly(2026, 9, 4), input.OccurredOn);
        Assert.Equal("Despesa \"Mercado\" de R$ 45,90 (Nubank, 04/09/2026) enviada para a caixa de entrada do Finances.", outcome.Message);
    }

    [Fact]
    public async Task Uses_the_only_account_when_none_is_named()
    {
        var sender = new ScriptedSender([Itau], []);

        await new RecordExpenseTool(sender, Time).ExecuteAsync(
            Context, Args("""{ "amount": 30, "description": "Uber", "date": "2026-09-03" }"""));

        var input = Assert.Single(sender.Created);
        Assert.Equal(Itau.Id, input.AccountId);
        Assert.Equal(new DateOnly(2026, 9, 3), input.OccurredOn);
    }

    [Fact]
    public async Task Asks_which_one_when_none_is_named_and_there_are_several()
    {
        var sender = new ScriptedSender([Itau], [Nubank]);

        var outcome = await new RecordExpenseTool(sender, Time).ExecuteAsync(
            Context, Args("""{ "amount": 30, "description": "Uber" }"""));

        Assert.False(outcome.Success);
        Assert.Equal("Pago com qual conta ou cartão? \"Itaú\", \"Nubank\"", outcome.Message);
        Assert.Empty(sender.Created);
    }

    [Fact]
    public void A_missing_or_non_positive_amount_is_unreadable() =>
        Assert.Throws<ArgumentException>(() =>
            new RecordExpenseTool(new ScriptedSender([], []), Time).Describe(Context, Args("""{ "amount": 0, "description": "x" }""")));
}
