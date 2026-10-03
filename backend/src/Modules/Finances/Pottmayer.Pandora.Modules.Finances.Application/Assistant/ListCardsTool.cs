using System.Text;
using System.Text.Json;
using Pottmayer.Pandora.Modules.Assistant.Abstractions.Commands;
using Pottmayer.Pandora.Modules.Finances.Application.Queries.GetCardAvailableLimit;
using Pottmayer.Pandora.Modules.Finances.Application.Queries.GetCards;
using Pottmayer.Pandora.Modules.Finances.Application.Queries.GetCardStatements;
using Pottmayer.Tars.Core.Mediator.Abstractions;

namespace Pottmayer.Pandora.Modules.Finances.Application.Assistant;

/// <summary>
/// <c>list_cards</c>: for each open credit card, the statement still being charged (open), any closed one not
/// yet paid off with its due date, and the limit left (<see cref="GetCardStatementsQuery"/>,
/// <see cref="GetCardAvailableLimitQuery"/>).
/// </summary>
public sealed class ListCardsTool(ISender sender) : IAssistantTool
{
    public AssistantCommandDescriptor Descriptor { get; } = new(
        Name: "list_cards",
        Description: "Shows the user their credit cards: the current statement, any unpaid closed statement with its due date, and the available limit. The numbers go to the user directly; you will not see them.",
        ParametersJsonSchema: """{ "type": "object", "properties": {} }""",
        Confirmation: ConfirmationPolicy.Never,
        Examples:
        [
            new AssistantCommandExample("how are my credit card bills?", "{}"),
            new AssistantCommandExample("how much limit do I have left?", "{}"),
        ]);

    public string Describe(AssistantToolContext context, JsonElement arguments) =>
        context.Text("Mostrar seus cartões?", "Show your cards?");

    public async Task<AssistantCommandOutcome> ExecuteAsync(AssistantToolContext context, JsonElement arguments, CancellationToken ct = default)
    {
        var cards = (await sender.Send(new GetCardsQuery(new GetCardsInput(context.UserId, IncludeArchived: false)), ct)).Value ?? [];
        var recap = $"[list_cards: {cards.Count} card(s) shown to the user; content withheld from you]";
        if (cards.Count == 0)
            return AssistantCommandOutcome.Ok(context.Text("Você ainda não tem cartões no Finances.", "You don't have cards in Finances yet."), recap);

        var sb = new StringBuilder(context.Text("Cartões:", "Cards:"));
        foreach (var card in cards)
        {
            string Money(decimal amount) => FinanceText.Money(context, amount, card.Currency);

            var statements = (await sender.Send(new GetCardStatementsQuery(new GetCardStatementsInput(context.UserId, card.Id)), ct)).Value ?? [];
            var limit = (await sender.Send(new GetCardAvailableLimitQuery(new GetCardAvailableLimitInput(context.UserId, card.Id)), ct)).Value;

            sb.Append("\n\n").Append(card.Name);
            foreach (var due in statements
                .Where(s => s.Status is "closed" or "partially-paid" or "overdue" && s.RemainingAmount > 0)
                .OrderBy(s => s.DueDate))
            {
                sb.Append("\n• ").Append(due.Status == "overdue"
                    ? context.Text($"Fatura vencida em {FinanceText.Day(context, due.DueDate)}: {Money(due.RemainingAmount)} a pagar",
                        $"Statement overdue since {FinanceText.Day(context, due.DueDate)}: {Money(due.RemainingAmount)} to pay")
                    : context.Text($"Fatura fechada, vence {FinanceText.Day(context, due.DueDate)}: {Money(due.RemainingAmount)} a pagar",
                        $"Closed statement, due {FinanceText.Day(context, due.DueDate)}: {Money(due.RemainingAmount)} to pay"));
            }
            if (statements.Where(s => s.Status == "open").MinBy(s => s.ClosingDate) is { } open)
                sb.Append("\n• ").Append(context.Text(
                    $"Fatura atual: {Money(open.TotalAmount)} (fecha {FinanceText.ShortDay(context, open.ClosingDate)}, vence {FinanceText.ShortDay(context, open.DueDate)})",
                    $"Current statement: {Money(open.TotalAmount)} (closes {FinanceText.ShortDay(context, open.ClosingDate)}, due {FinanceText.ShortDay(context, open.DueDate)})"));
            if (limit?.AvailableLimit is { } available)
                sb.Append("\n• ").Append(context.Text($"Limite disponível: {Money(available)}", $"Available limit: {Money(available)}"));
        }

        return AssistantCommandOutcome.Ok(sb.ToString(), recap);
    }
}
