using System.Text.Json;
using Pottmayer.Pandora.Modules.Agenda.Application.Commands;
using Pottmayer.Pandora.Modules.Agenda.Application.Dtos;
using Pottmayer.Pandora.Modules.Agenda.Application.Queries.GetEvent;
using Pottmayer.Pandora.Modules.Agenda.Application.Queries.GetEvents;
using Pottmayer.Pandora.Modules.Agenda.Domain.ValueObjects;
using Pottmayer.Pandora.Modules.Assistant.Abstractions.Commands;
using Pottmayer.Tars.Core.Mediator.Abstractions;
using static Pottmayer.Pandora.Modules.Assistant.Abstractions.Commands.ToolArguments;

namespace Pottmayer.Pandora.Modules.Agenda.Application.Assistant;

/// <summary>
/// The event occurrence an edit or delete is about, for <see cref="UpdateEventTool"/> and
/// <see cref="DeleteEventTool"/>: the one the user pointed at on a list (<c>ref</c>), or the next one whose
/// title matches their words (<c>event</c>), on the day they said (<c>on</c>) or in the coming months.
/// Also settles the edit scope: a one-off event is always edited whole.
/// </summary>
internal static class EventTarget
{
    /// <summary>How far ahead a name is looked for when no day was said.</summary>
    private static readonly TimeSpan Horizon = TimeSpan.FromDays(120);

    public const string SchemaProperties = """
            "ref": { "type": "integer", "description": "The event's number on the last list shown, when the user points at it by number." },
            "event": { "type": "string", "description": "The event as the user named it (e.g. \"dentista\"), when not pointing by number." },
            "on": { "type": "string", "description": "The day of the occurrence the user means (bare date), when they said it." },
            "scope": { "type": "string", "enum": ["this", "following", "all"], "description": "For a repeating event: just this occurrence (default), this and the following ones, or all of them." }
        """;

    public sealed record Target(EventOccurrenceDto Occurrence, EventEditScope Scope, DateTimeOffset? OccurrenceStart, bool IsSeries);

    public static async Task<(Target? Match, string? Problem)> FindAsync(
        ISender sender, AssistantToolContext context, JsonElement arguments, DateTimeOffset now, CancellationToken ct)
    {
        var (occurrence, problem) = await OccurrenceAsync(sender, context, arguments, now, ct);
        if (occurrence is null)
            return (null, problem);

        var series = await sender.Send(new GetEventQuery(new GetEventInput(context.UserId, occurrence.EventId)), ct);
        if (!series.IsSuccess)
            return (null, string.Join("; ", series.Errors.Select(e => e.Message)));

        // The scope only means something for a series; a one-off event is its own whole.
        if (series.Value!.Rrule is null)
            return (new Target(occurrence, EventEditScope.All, null, IsSeries: false), null);

        var scope = OptionalString(arguments, "scope") switch
        {
            "all" => EventEditScope.All,
            "following" => EventEditScope.ThisAndFuture,
            _ => EventEditScope.This,
        };
        return (new Target(occurrence, scope, scope == EventEditScope.All ? null : occurrence.OriginalStartsAt, IsSeries: true), null);
    }

    /// <summary>A found occurrence as the item a confirmation pins (<see cref="IAssistantTargetedTool"/>).</summary>
    public static (ListedItem? Target, string? Problem) AsTarget((Target? Match, string? Problem) found) =>
        found.Match is { } t
            ? (new ListedItem("event", t.Occurrence.EventId, t.Occurrence.Title, t.Occurrence.StartsAt), null)
            : (null, found.Problem);

    /// <summary>"Dentista" or "Dentista (05/09/2026 às 09:00)" — for the confirmation question.</summary>
    public static string Name(AssistantToolContext context, JsonElement arguments)
    {
        if (OptionalRef(context, arguments, "event") is { } pinned)
            return pinned.At is { } at
                ? $"{pinned.Label} ({FormatDayOrInstant(context, at, TimeZoneInfo.ConvertTime(at, context.TimeZone).TimeOfDay != TimeSpan.Zero)})"
                : pinned.Label;

        var name = TargetName(arguments, "event");
        return OptionalDayOrInstant(context, arguments, "on") is { } on
            ? $"{name} ({FormatDayOrInstant(context, on.At, hasTime: false)})"
            : name;
    }

    /// <summary>" (05/09/2026 às 09:00)", " (… e as seguintes)" or " (todas as ocorrências)" — what the change reached.</summary>
    public static string Reach(AssistantToolContext context, Target target)
    {
        var when = target.Occurrence.IsAllDay
            ? FormatDayOrInstant(context, target.Occurrence.StartsAt, hasTime: false)
            : context.FormatDateTime(target.Occurrence.StartsAt);
        return target.Scope switch
        {
            EventEditScope.All when target.IsSeries => context.Text(" (todas as ocorrências)", " (every occurrence)"),
            EventEditScope.ThisAndFuture => context.Text($" ({when} e as seguintes)", $" ({when} and the following ones)"),
            _ => $" ({when})",
        };
    }

    private static async Task<(EventOccurrenceDto? Match, string? Problem)> OccurrenceAsync(
        ISender sender, AssistantToolContext context, JsonElement arguments, DateTimeOffset now, CancellationToken ct)
    {
        if (OptionalRef(context, arguments, "event") is { } pinned)
        {
            var at = pinned.At ?? now;
            var around = await EventsAsync(sender, context, at.AddDays(-1), at.AddDays(1), ct);
            var match = around.FirstOrDefault(o => o.EventId == pinned.Id && (pinned.At is null || o.StartsAt == pinned.At));
            return match is not null
                ? (match, null)
                : (null, context.Text($"\"{pinned.Label}\" não está mais na agenda.", $"\"{pinned.Label}\" is no longer on the agenda."));
        }

        var name = OptionalString(arguments, "event")
            ?? throw new ArgumentException($"Pass either '{ListedRefs.Ref}' or 'event'.");

        var on = OptionalDayOrInstant(context, arguments, "on");
        var from = on?.At ?? now;
        var to = on is not null ? from.AddDays(1) : from + Horizon;
        var occurrences = await EventsAsync(sender, context, from, to, ct);

        // A series shows up once per occurrence; the name picks the event, the earliest occurrence stands for it.
        var firsts = occurrences.GroupBy(o => o.EventId).Select(g => g.MinBy(o => o.StartsAt)!).ToList();
        if (on is null)
            return PickByTitle(context, firsts, o => o.Title, name, "seus próximos eventos", "your upcoming events");

        var day = FormatDayOrInstant(context, on.Value.At, hasTime: false);
        return PickByTitle(context, firsts, o => o.Title, name, $"seus eventos de {day}", $"your events on {day}");
    }

    private static async Task<IReadOnlyList<EventOccurrenceDto>> EventsAsync(
        ISender sender, AssistantToolContext context, DateTimeOffset from, DateTimeOffset to, CancellationToken ct)
    {
        var result = await sender.Send(new GetEventsQuery(new GetEventsInput(context.UserId, from, to, null)), ct);
        return [.. (result.Value ?? []).Where(o => o.Status != nameof(EventStatus.Cancelled))];
    }
}
