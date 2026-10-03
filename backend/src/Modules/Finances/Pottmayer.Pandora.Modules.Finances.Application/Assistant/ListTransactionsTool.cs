using System.Text;
using System.Text.Json;
using Pottmayer.Pandora.Modules.Assistant.Abstractions.Commands;
using Pottmayer.Tars.Core.Mediator.Abstractions;
using static Pottmayer.Pandora.Modules.Assistant.Abstractions.Commands.ToolArguments;

namespace Pottmayer.Pandora.Modules.Finances.Application.Assistant;

/// <summary>
/// <c>list_transactions</c>: the user's entries matching a filter (<see cref="TransactionSearch"/>), latest or
/// largest first, with the total of everything that matched. Read-only — the list goes straight to the
/// user and the history keeps a content-free recap. The lines are bullets, not numbers: nothing in Finances
/// is changed from the chat.
/// </summary>
public sealed class ListTransactionsTool(ISender sender) : IAssistantTool
{
    private const int DefaultLimit = 10;
    private const int MaxLimit = 30;

    public AssistantCommandDescriptor Descriptor { get; } = new(
        Name: "list_transactions",
        Description: "Shows the user their financial transactions matching a filter (period, kind, account or card, category, text), latest or largest first, with the total. The list goes to the user directly; you will not see it.",
        ParametersJsonSchema: $$"""
        {
          "type": "object",
          "properties": {
            {{TransactionSearch.SchemaProperties}},
            "kind": { "type": "string", "enum": ["expense", "income"], "description": "Only expenses or only income, when the user said so. Omit for both." },
            "order": { "type": "string", "enum": ["recent", "largest"], "description": "Latest first (default) or largest amounts first." },
            "limit": { "type": "integer", "description": "How many to show (default 10, at most 30)." }
          }
        }
        """,
        Confirmation: ConfirmationPolicy.Never,
        Examples:
        [
            new AssistantCommandExample("what did I spend on iFood this month?",
                """{ "from": "2026-09-01", "to": "2026-09-04", "kind": "expense", "text": "ifood" }"""),
            new AssistantCommandExample("my 5 biggest expenses in August on the Nubank card",
                """{ "from": "2026-08-01", "to": "2026-08-31", "kind": "expense", "card": "Nubank", "order": "largest", "limit": 5 }"""),
            new AssistantCommandExample("last transactions", "{}"),
        ]);

    public string Describe(AssistantToolContext context, JsonElement arguments) =>
        context.Text("Mostrar seus lançamentos?", "Show your transactions?");

    public async Task<AssistantCommandOutcome> ExecuteAsync(AssistantToolContext context, JsonElement arguments, CancellationToken ct = default)
    {
        var filter = TransactionSearch.Parse(arguments, defaultKind: null);
        var limit = arguments.TryGetProperty("limit", out var l) && l.TryGetInt32(out var n) ? Math.Clamp(n, 1, MaxLimit) : DefaultLimit;
        var largest = OptionalString(arguments, "order") == "largest";

        var (found, problem) = await TransactionSearch.RunAsync(sender, context, filter, ct);
        if (found is null)
            return AssistantCommandOutcome.Failed(problem!);

        var items = found.Items;
        var recap = $"[list_transactions: {items.Count} transaction(s) shown to the user; content withheld from you]";
        var period = TransactionSearch.Period(context, filter);
        if (items.Count == 0)
            return AssistantCommandOutcome.Ok(context.Text($"Nenhum lançamento encontrado{period}.", $"No transactions found{period}."), recap);

        var shown = largest
            ? items.OrderByDescending(t => t.Amount).Take(limit)
            : items.OrderByDescending(t => t.OccurredOn).Take(limit);

        var sb = new StringBuilder(context.Text($"Lançamentos{period}:", $"Transactions{period}:"));
        foreach (var t in shown)
        {
            sb.Append("\n• ").Append(FinanceText.ShortDay(context, t.OccurredOn))
              .Append(" · ").Append(t.Description)
              .Append(" · ").Append(t.Kind == "income" ? "+" : "").Append(FinanceText.Money(context, t.Amount, t.Currency));
            if (found.PlaceOf(t) is { } place)
                sb.Append(" · ").Append(place);
        }

        // A total only adds up when the entries go the same way.
        var kinds = items.Select(t => t.Kind).Distinct().Count();
        var currency = items[0].Currency;
        sb.Append("\n\n").Append(items.Count > limit
            ? context.Text($"Mostrando {limit} de {items.Count}.", $"Showing {limit} of {items.Count}.")
            : context.Text($"{items.Count} lançamento(s).", $"{items.Count} transaction(s)."));
        if (kinds == 1 && items.All(t => t.Currency == currency))
            sb.Append(context.Text(" Total: ", " Total: ")).Append(FinanceText.Money(context, items.Sum(t => t.Amount), currency)).Append('.');
        if (found.Truncated)
            sb.Append('\n').Append(context.Text("(Período longo demais; li só os mais recentes.)", "(Period too long; read only the most recent ones.)"));

        return AssistantCommandOutcome.Ok(sb.ToString(), recap);
    }
}
