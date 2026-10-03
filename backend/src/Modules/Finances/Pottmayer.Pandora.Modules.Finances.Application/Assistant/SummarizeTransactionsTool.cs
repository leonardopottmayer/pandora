using System.Text;
using System.Text.Json;
using Pottmayer.Pandora.Modules.Assistant.Abstractions.Commands;
using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Tars.Core.Mediator.Abstractions;
using static Pottmayer.Pandora.Modules.Assistant.Abstractions.Commands.ToolArguments;

namespace Pottmayer.Pandora.Modules.Finances.Application.Assistant;

/// <summary>
/// <c>summarize_transactions</c>: how much the user spent (or received) in a period, optionally broken down by
/// category, account, month or description, and compared with the period before. Every number is computed
/// here; the model only picks the filter and the breakdown, and never sees the result.
/// </summary>
public sealed class SummarizeTransactionsTool(ISender sender, TimeProvider timeProvider) : IAssistantTool
{
    /// <summary>Groups shown before the rest is folded into "others".</summary>
    private const int MaxGroups = 10;

    public AssistantCommandDescriptor Descriptor { get; } = new(
        Name: "summarize_transactions",
        Description: "Totals the user's expenses (or income) in a period, optionally grouped by category, account, month or description, and compared with the previous period. The numbers go to the user directly; you will not see them. Use for \"how much did I spend\" questions.",
        ParametersJsonSchema: $$"""
        {
          "type": "object",
          "properties": {
            {{TransactionSearch.SchemaProperties}},
            "kind": { "type": "string", "enum": ["expense", "income"], "description": "Expenses (default) or income." },
            "groupBy": { "type": "string", "enum": ["category", "account", "month", "description"], "description": "How to break the total down, when the user asked for a breakdown." },
            "comparePrevious": { "type": "boolean", "description": "True to compare with the period of the same length just before (e.g. September vs August)." }
          }
        }
        """,
        Confirmation: ConfirmationPolicy.Never,
        Examples:
        [
            new AssistantCommandExample("how much did I spend in September?",
                """{ "from": "2026-09-01", "to": "2026-09-30" }"""),
            new AssistantCommandExample("spending by category this month compared to last month",
                """{ "from": "2026-09-01", "to": "2026-09-30", "groupBy": "category", "comparePrevious": true }"""),
            new AssistantCommandExample("how much went to restaurants this year, month by month?",
                """{ "from": "2026-01-01", "to": "2026-12-31", "category": "restaurants", "groupBy": "month" }"""),
        ]);

    public string Describe(AssistantToolContext context, JsonElement arguments) =>
        context.Text("Resumir seus lançamentos?", "Summarize your transactions?");

    public async Task<AssistantCommandOutcome> ExecuteAsync(AssistantToolContext context, JsonElement arguments, CancellationToken ct = default)
    {
        var filter = TransactionSearch.Parse(arguments, defaultKind: "expense");
        // No period said: this month so far.
        if (filter.From is null && filter.To is null)
        {
            var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), context.TimeZone).DateTime);
            filter = filter with { From = new DateOnly(today.Year, today.Month, 1), To = today };
        }

        var groupBy = OptionalString(arguments, "groupBy");
        var compare = OptionalBool(arguments, "comparePrevious") && filter.From is not null && filter.To is not null;

        var (found, problem) = await TransactionSearch.RunAsync(sender, context, filter, ct);
        if (found is null)
            return AssistantCommandOutcome.Failed(problem!);

        TransactionSearch.Filter? before = null;
        TransactionSearch.Found? previous = null;
        if (compare)
        {
            before = Previous(filter);
            (previous, problem) = await TransactionSearch.RunAsync(sender, context, before, ct);
            if (previous is null)
                return AssistantCommandOutcome.Failed(problem!);
        }

        var recap = "[summarize_transactions: totals shown to the user; content withheld from you]";
        var expense = filter.Kind == "expense";
        var currency = found.Items.FirstOrDefault()?.Currency ?? previous?.Items.FirstOrDefault()?.Currency ?? "BRL";
        string Money(decimal amount) => FinanceText.Money(context, amount, currency);

        var total = found.Items.Sum(t => t.Amount);
        var sb = new StringBuilder()
            .Append(expense ? context.Text("Gastos", "Spending") : context.Text("Receitas", "Income"))
            .Append(TransactionSearch.Period(context, filter)).Append(": ").Append(Money(total))
            .Append(context.Text($" ({found.Items.Count} lançamento(s))", $" ({found.Items.Count} transaction(s))"));

        if (previous is not null)
        {
            var previousTotal = previous.Items.Sum(t => t.Amount);
            sb.Append('\n').Append(context.Text("Antes", "Before")).Append(TransactionSearch.Period(context, before!))
              .Append(": ").Append(Money(previousTotal)).Append(Change(total, previousTotal));
        }

        if (groupBy is not null && found.Items.Count > 0)
        {
            Func<TransactionDto, string> key = groupBy switch
            {
                "category" => t => found.CategoryOf(t) ?? context.Text("Sem categoria", "Uncategorized"),
                "account" => t => found.PlaceOf(t) ?? "—",
                "month" => t => new DateOnly(t.OccurredOn.Year, t.OccurredOn.Month, 1)
                    .ToString(context.IsPortuguese ? "MMM/yyyy" : "MMM yyyy", FinanceText.Culture(context)),
                _ => t => t.Description.Trim(),
            };
            var previousByKey = previous?.Items.GroupBy(key).ToDictionary(g => g.Key, g => g.Sum(t => t.Amount));

            var groups = found.Items.GroupBy(key)
                .Select(g => (Key: g.Key, Amount: g.Sum(t => t.Amount), First: g.Min(t => t.OccurredOn)))
                .ToList();
            // Months read in order; everything else, largest first.
            groups = groupBy == "month"
                ? [.. groups.OrderBy(g => g.First)]
                : [.. groups.OrderByDescending(g => g.Amount)];

            sb.Append("\n\n").Append(groupBy switch
            {
                "category" => context.Text("Por categoria:", "By category:"),
                "account" => context.Text("Por conta/cartão:", "By account/card:"),
                "month" => context.Text("Por mês:", "By month:"),
                _ => context.Text("Por descrição:", "By description:"),
            });
            foreach (var g in groups.Take(MaxGroups))
            {
                sb.Append("\n• ").Append(g.Key).Append(" — ").Append(Money(g.Amount));
                if (previousByKey is not null)
                    sb.Append(Change(g.Amount, previousByKey.GetValueOrDefault(g.Key)));
                else if (total > 0)
                    sb.Append($" ({Math.Round(g.Amount * 100 / total):0}%)");
            }
            if (groups.Count > MaxGroups)
                sb.Append("\n• ").Append(context.Text("Outros", "Others")).Append(" — ")
                  .Append(Money(groups.Skip(MaxGroups).Sum(g => g.Amount)));
        }

        if (found.Truncated)
            sb.Append('\n').Append(context.Text("(Período longo demais; somei só os mais recentes.)", "(Period too long; added up only the most recent ones.)"));

        return AssistantCommandOutcome.Ok(sb.ToString(), recap);

        string Change(decimal now, decimal then) =>
            then == 0 ? (now == 0 ? "" : context.Text(" (antes: nada)", " (before: nothing)"))
            : $" ({(now - then) / then:+0%;-0%;0%})";
    }

    /// <summary>
    /// The period just before, of the same length — whole months when the filter is whole months, so
    /// September compares with August (31 vs 30 days) and a quarter with the quarter before.
    /// </summary>
    private static TransactionSearch.Filter Previous(TransactionSearch.Filter filter)
    {
        var from = filter.From!.Value;
        var to = filter.To!.Value;
        if (from.Day == 1 && to.AddDays(1).Day == 1)
        {
            var months = (to.Year - from.Year) * 12 + to.Month - from.Month + 1;
            return filter with { From = from.AddMonths(-months), To = from.AddDays(-1) };
        }

        var days = to.DayNumber - from.DayNumber + 1;
        return filter with { From = from.AddDays(-days), To = from.AddDays(-1) };
    }
}
