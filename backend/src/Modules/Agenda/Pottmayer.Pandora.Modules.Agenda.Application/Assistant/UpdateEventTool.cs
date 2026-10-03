using System.Globalization;
using System.Text.Json;
using Pottmayer.Pandora.Modules.Agenda.Application.Commands.UpdateEvent;
using Pottmayer.Pandora.Modules.Assistant.Abstractions.Commands;
using Pottmayer.Tars.Core.Mediator.Abstractions;
using static Pottmayer.Pandora.Modules.Assistant.Abstractions.Commands.ToolArguments;

namespace Pottmayer.Pandora.Modules.Agenda.Application.Assistant;

/// <summary>
/// <c>update_event</c>: renames, moves or relocates an event (<see cref="UpdateEventCommand"/>). The event
/// is found here (<see cref="EventTarget"/>); a new start keeps the event's length unless a new end is
/// given, and a bare day makes it all-day. A repeating event changes only the occurrence meant, unless
/// the user said otherwise.
/// </summary>
public sealed class UpdateEventTool(ISender sender, TimeProvider timeProvider) : IAssistantTargetedTool
{
    public AssistantCommandDescriptor Descriptor { get; } = new(
        Name: "update_event",
        Description: "Changes one of the user's calendar events: its title, time or place. You cannot see the events: point at it by its number on the last list, or pass the words the user used to name it.",
        ParametersJsonSchema: $$"""
        {
          "type": "object",
          "properties": {
            {{EventTarget.SchemaProperties}},
            "title": { "type": "string", "description": "The new title, only when the user renames it." },
            "startsAt": { "type": "string", "description": "The new start when the user gave a day: an ISO-8601 timestamp with offset, or a bare date for an all-day event." },
            "time": { "type": "string", "description": "The new start time (\"20:00\") when the user gave only a time — the event keeps its day." },
            "endsAt": { "type": "string", "description": "The new end (ISO-8601 with offset), only when the user said it; otherwise the length is kept." },
            "location": { "type": "string", "description": "The new place, only when the user said it." }
          }
        }
        """,
        Confirmation: ConfirmationPolicy.WhenAmbiguous,
        Examples:
        [
            new AssistantCommandExample(
                "move the dentist to Thursday at 3pm",
                """{ "event": "dentist", "startsAt": "2026-09-10T15:00:00-03:00" }"""),
            new AssistantCommandExample(
                "change 2 to 10am",
                """{ "ref": 2, "startsAt": "2026-09-05T10:00:00-03:00" }"""),
            new AssistantCommandExample(
                "the dinner with Ana is at 8pm now",
                """{ "event": "dinner Ana", "time": "20:00" }"""),
            new AssistantCommandExample(
                "from now on football is at 7pm",
                """{ "event": "football", "time": "19:00", "scope": "following" }"""),
            new AssistantCommandExample(
                "the weekly sync is at room 4 from now on",
                """{ "event": "weekly sync", "location": "Room 4", "scope": "following" }"""),
        ]);

    public string Describe(AssistantToolContext context, JsonElement arguments)
    {
        var changes = Changes(context, Parse(context, arguments));
        return context.Text(
            $"Alterar o evento \"{EventTarget.Name(context, arguments)}\"{changes}?",
            $"Change the event \"{EventTarget.Name(context, arguments)}\"{changes}?");
    }

    public async Task<AssistantCommandOutcome> ExecuteAsync(AssistantToolContext context, JsonElement arguments, CancellationToken ct = default)
    {
        var change = Parse(context, arguments);
        if (change.Title is null && change.Start is null && change.Time is null && change.EndsAt is null && change.Location is null)
            return AssistantCommandOutcome.Failed(context.Text("O que devo mudar no evento?", "What should I change on the event?"));

        var (target, problem) = await EventTarget.FindAsync(sender, context, arguments, timeProvider.GetUtcNow(), ct);
        if (target is null)
            return AssistantCommandOutcome.Failed(problem!);

        var occurrence = target.Occurrence;
        DateTimeOffset? startsAt = null, endsAt = change.EndsAt;
        bool? isAllDay = null;
        if ((change.Start ?? OnSameDay(context, occurrence.StartsAt, change.Time)) is { } start)
        {
            startsAt = start.At;
            isAllDay = !start.HasTime;
            var length = !start.HasTime ? TimeSpan.FromDays(1)
                : occurrence.IsAllDay ? TimeSpan.FromHours(1)
                : occurrence.EndsAt - occurrence.StartsAt;
            endsAt ??= start.At + length;
        }

        var result = await sender.Send(new UpdateEventCommand(new UpdateEventInput(
            context.UserId, occurrence.EventId, target.Scope, target.OccurrenceStart,
            change.Title, Description: null, change.Location, Url: null, startsAt, endsAt, isAllDay, CalendarId: null)), ct);
        if (!result.IsSuccess)
            return AssistantCommandOutcome.Failed(string.Join("; ", result.Errors.Select(e => e.Message)));

        var title = change.Title ?? occurrence.Title;
        return AssistantCommandOutcome.Ok(context.Text(
            $"Evento \"{title}\"{EventTarget.Reach(context, target)} alterado{Changes(context, change)}.",
            $"Event \"{title}\"{EventTarget.Reach(context, target)} changed{Changes(context, change)}."));
    }

    private sealed record Change(
        string? Title, (DateTimeOffset At, bool HasTime)? Start, TimeOnly? Time, DateTimeOffset? EndsAt, string? Location);

    private static Change Parse(AssistantToolContext context, JsonElement arguments) => new(
        OptionalString(arguments, "title"),
        OptionalDayOrInstant(context, arguments, "startsAt"),
        OptionalString(arguments, "time") is { } time
            ? TimeOnly.ParseExact(time, ["HH:mm", "H:mm", "HH:mm:ss"], CultureInfo.InvariantCulture)
            : null,
        OptionalInstant(arguments, "endsAt"),
        OptionalString(arguments, "location"));

    /// <summary><paramref name="time"/> on the local day the occurrence starts, in the user's zone.</summary>
    private static (DateTimeOffset At, bool HasTime)? OnSameDay(AssistantToolContext context, DateTimeOffset occurrence, TimeOnly? time)
    {
        if (time is not { } t)
            return null;
        var local = TimeZoneInfo.ConvertTime(occurrence, context.TimeZone).Date + t.ToTimeSpan();
        return (new DateTimeOffset(local, context.TimeZone.GetUtcOffset(local)), true);
    }

    /// <summary>": para 10/09/2026 às 15:00, em Sala 4" — what is being changed, in the user's words.</summary>
    private static string Changes(AssistantToolContext context, Change change)
    {
        var parts = new List<string>();
        if (change.Title is { } title)
            parts.Add(context.Text($"título \"{title}\"", $"title \"{title}\""));
        if (change.Start is { } start)
            parts.Add(context.Text("para ", "to ") + FormatDayOrInstant(context, start.At, start.HasTime));
        else if (change.Time is { } time)
            parts.Add(context.Text($"para as {time:HH:mm}", $"to {time:HH:mm}"));
        if (change.EndsAt is { } end)
            parts.Add(context.Text("até ", "until ") + context.FormatDateTime(end));
        if (change.Location is { } location)
            parts.Add(context.Text($"em {location}", $"at {location}"));
        return parts.Count == 0 ? "" : ": " + string.Join(", ", parts);
    }

    public async Task<(ListedItem? Target, string? Problem)> FindTargetAsync(
        AssistantToolContext context, JsonElement arguments, CancellationToken ct = default) =>
        EventTarget.AsTarget(await EventTarget.FindAsync(sender, context, arguments, timeProvider.GetUtcNow(), ct));
}
