using System.Globalization;
using System.Text;
using System.Text.Json;
using Pottmayer.Pandora.Modules.Agenda.Application.Dtos;
using Pottmayer.Pandora.Modules.Agenda.Application.Queries.GetToday;
using Pottmayer.Pandora.Modules.Agenda.Domain.ValueObjects;
using Pottmayer.Pandora.Modules.Assistant.Abstractions.Commands;
using Pottmayer.Tars.Core.Mediator.Abstractions;
using static Pottmayer.Pandora.Modules.Assistant.Abstractions.Commands.ToolArguments;

namespace Pottmayer.Pandora.Modules.Agenda.Application.Assistant;

/// <summary>
/// <c>list_agenda</c>: the user's events, tasks due and reminders for a day or a span of days, read through
/// <see cref="GetTodayQuery"/>. The list is formatted here and goes straight to the user; the conversation
/// history keeps only a content-free recap (<see cref="AssistantCommandOutcome.Recap"/>), so no title ever
/// reaches the hosted model.
/// </summary>
public sealed class ListAgendaTool(ISender sender) : IAssistantTool
{
    /// <summary>The widest span one request lists — a month; beyond that the reply stops being readable.</summary>
    private const int MaxDays = 31;

    public AssistantCommandDescriptor Descriptor { get; } = new(
        Name: "list_agenda",
        Description: "Shows the user their agenda (events, tasks due and reminders) for a day or a span of days. The list goes to the user directly; you will not see it.",
        ParametersJsonSchema: """
        {
          "type": "object",
          "properties": {
            "from": { "type": "string", "description": "The first day to list, as a bare date (2026-09-05)." },
            "to": { "type": "string", "description": "The last day to list, inclusive (bare date). Omit for a single day." }
          },
          "required": ["from"]
        }
        """,
        Confirmation: ConfirmationPolicy.Never,
        Examples:
        [
            new AssistantCommandExample(
                "what do I have tomorrow?",
                """{ "from": "2026-09-05" }"""),
            new AssistantCommandExample(
                "what's on my agenda this week?",
                """{ "from": "2026-09-01", "to": "2026-09-07" }"""),
        ]);

    public string Describe(AssistantToolContext context, JsonElement arguments)
    {
        var (from, to) = Parse(context, arguments);
        var span = FormatSpan(context, from, to);
        return context.Text($"Mostrar a agenda de {span}?", $"Show the agenda for {span}?");
    }

    public async Task<AssistantCommandOutcome> ExecuteAsync(AssistantToolContext context, JsonElement arguments, CancellationToken ct = default)
    {
        var (from, to) = Parse(context, arguments);
        var span = FormatSpan(context, from, to);

        if (to.DayNumber - from.DayNumber + 1 > MaxDays)
            return AssistantCommandOutcome.Failed(context.Text(
                $"Consigo listar até {MaxDays} dias por vez.", $"I can list up to {MaxDays} days at a time."));

        var result = await sender.Send(new GetTodayQuery(new GetTodayInput(context.UserId, from, to)), ct);
        if (!result.IsSuccess)
            return AssistantCommandOutcome.Failed(string.Join("; ", result.Errors.Select(e => e.Message)));

        var items = (result.Value ?? []).Where(IsOpen).ToList();
        var recap = $"[list_agenda: {items.Count} item(s) for {from:yyyy-MM-dd}..{to:yyyy-MM-dd} shown to the user; content withheld from you]";

        if (items.Count == 0)
            return AssistantCommandOutcome.Ok(context.Text($"Nada na agenda em {span}.", $"Nothing on the agenda for {span}."), recap);

        var culture = Culture(context);
        var sb = new StringBuilder(context.Text($"Agenda de {span}:", $"Agenda for {span}:"));

        // Grouped by the user's local day; an event that began before the span shows on its first day. The
        // lines are numbered across days, so the user can then point at one ("cancela o 2").
        var listed = new List<ListedItem>(items.Count);
        foreach (var day in items.GroupBy(i => Max(LocalDay(context, i.At), from)).OrderBy(g => g.Key))
        {
            if (from != to)
                sb.Append("\n\n").Append(day.Key.ToString(context.IsPortuguese ? "ddd, dd/MM" : "ddd, MMM d", culture));
            foreach (var item in day)
            {
                listed.Add(new ListedItem(item.Kind, item.Id, item.Title, item.Kind == "event" ? item.At : null));
                sb.Append('\n').Append(listed.Count).Append(". ").Append(Line(context, item));
            }
        }

        return AssistantCommandOutcome.Ok(sb.ToString(), recap, listed);
    }

    private static string Line(AssistantToolContext context, TodayItemDto item)
    {
        var at = Time(context, item.At);
        return item.Kind switch
        {
            "event" when item.IsAllDay => context.Text($"dia todo · {item.Title}", $"all day · {item.Title}"),
            "event" when item.EndsAt is { } end => $"{at}–{Time(context, end)} {item.Title}",
            "event" => $"{at} {item.Title}",
            "task" when item.IsAllDay => context.Text($"tarefa · {item.Title}", $"task · {item.Title}"),
            "task" => context.Text($"{at} tarefa · {item.Title}", $"{at} task · {item.Title}"),
            _ => context.Text($"{at} lembrete · {item.Title}", $"{at} reminder · {item.Title}"),
        };
    }

    /// <summary>Cancelled items, finished tasks and acknowledged reminders are not on the agenda anymore.</summary>
    private static bool IsOpen(TodayItemDto item) =>
        item.Status is not (nameof(EventStatus.Cancelled) or nameof(TaskItemStatus.Done) or nameof(ReminderStatus.Acknowledged));

    private static (DateOnly From, DateOnly To) Parse(AssistantToolContext context, JsonElement arguments)
    {
        var from = OptionalDayOrInstant(context, arguments, "from") is { } f
            ? LocalDay(context, f.At)
            : throw new ArgumentException("The 'from' argument is required.");
        var to = OptionalDayOrInstant(context, arguments, "to") is { } t ? LocalDay(context, t.At) : from;
        return to < from ? (to, from) : (from, to);
    }

    private static DateOnly LocalDay(AssistantToolContext context, DateTimeOffset at) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(at, context.TimeZone).DateTime);

    private static DateOnly Max(DateOnly a, DateOnly b) => a > b ? a : b;

    private static string Time(AssistantToolContext context, DateTimeOffset at) =>
        TimeZoneInfo.ConvertTime(at, context.TimeZone).ToString("HH:mm", CultureInfo.InvariantCulture);

    private static string FormatSpan(AssistantToolContext context, DateOnly from, DateOnly to)
    {
        var format = context.IsPortuguese ? "dd/MM/yyyy" : "MMM d, yyyy";
        var culture = Culture(context);
        return from == to
            ? from.ToString(format, culture)
            : context.Text($"{from.ToString(format, culture)} a {to.ToString(format, culture)}",
                $"{from.ToString(format, culture)} to {to.ToString(format, culture)}");
    }

    private static CultureInfo Culture(AssistantToolContext context) =>
        CultureInfo.GetCultureInfo(context.IsPortuguese ? "pt-BR" : "en-US");
}
