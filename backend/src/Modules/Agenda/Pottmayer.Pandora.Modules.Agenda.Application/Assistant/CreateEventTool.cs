using System.Text.Json;
using Pottmayer.Pandora.Modules.Agenda.Application.Commands.CreateEvent;
using Pottmayer.Pandora.Modules.Agenda.Application.Queries.GetCalendars;
using Pottmayer.Pandora.Modules.Assistant.Abstractions.Commands;
using Pottmayer.Tars.Core.Mediator.Abstractions;
using static Pottmayer.Pandora.Modules.Assistant.Abstractions.Commands.ToolArguments;
using Pottmayer.Pandora.Modules.Agenda.Abstractions;

namespace Pottmayer.Pandora.Modules.Agenda.Application.Assistant;

/// <summary>
/// <c>create_event</c>: an event, single or recurring, in the calendar the user named or else their default
/// one (the first writable one when none is marked default), forwarded to <see cref="CreateEventCommand"/>.
/// Without an end it lasts an hour; a bare start date makes it an all-day event.
/// </summary>
public sealed class CreateEventTool(ISender sender) : IAssistantTool
{
    public IReadOnlyList<string> Permissions { get; } = [AgendaPermissions.Events.Create, AgendaPermissions.Calendars.Read];

    public AssistantCommandDescriptor Descriptor { get; } = new(
        Name: "create_event",
        Description: "Creates a calendar event (an appointment, meeting or something happening at a time or on a day). Use when the user schedules something, not when they want to be pinged (create_reminder) or note a to-do (create_task).",
        ParametersJsonSchema: $$"""
        {
          "type": "object",
          "properties": {
            "title": { "type": "string", "description": "What the event is, in a few words." },
            "startsAt": { "type": "string", "description": "When it starts: an ISO-8601 timestamp with offset, or a bare date (2026-09-05) for an all-day event." },
            "endsAt": { "type": "string", "description": "When it ends, same format as startsAt. Omit when the user did not say." },
            "location": { "type": "string", "description": "Where, only when the user said." },
            "description": { "type": "string", "description": "Details or notes about the event, only when the user gave them." },
            "calendar": { "type": "string", "description": "The calendar's name as the user said it (\"Work\", \"Família\"), only when they named one." },
            {{AgendaToolArguments.RecurrenceSchema}}
          },
          "required": ["title", "startsAt"]
        }
        """,
        Confirmation: ConfirmationPolicy.WhenAmbiguous,
        Examples:
        [
            new AssistantCommandExample(
                "dentist appointment next Tuesday at 3pm",
                """{ "title": "Dentist", "startsAt": "2026-09-08T15:00:00-03:00" }"""),
            new AssistantCommandExample(
                "put João's birthday party on Saturday on the calendar, at his place",
                """{ "title": "João's birthday party", "startsAt": "2026-09-12", "location": "João's place" }"""),
            new AssistantCommandExample(
                "football every Thursday at 7pm on the Personal calendar",
                """{ "title": "Football", "startsAt": "2026-09-10T19:00:00-03:00", "calendar": "Personal", "recurrence": "FREQ=WEEKLY;BYDAY=TH" }"""),
        ]);

    public string Describe(AssistantToolContext context, JsonElement arguments)
    {
        var e = Parse(context, arguments);
        var when = FormatDayOrInstant(context, e.StartsAt, !e.IsAllDay);
        var repeats = AgendaToolArguments.Repeats(context, e.Rrule);
        return context.Text($"Criar o evento \"{e.Title}\" em {when}{repeats}?", $"Create the event \"{e.Title}\" on {when}{repeats}?");
    }

    public async Task<AssistantCommandOutcome> ExecuteAsync(AssistantToolContext context, JsonElement arguments, CancellationToken ct = default)
    {
        var e = Parse(context, arguments);

        var calendars = await sender.Send(new GetCalendarsQuery(new GetCalendarsInput(context.UserId)), ct);
        // A Google calendar the account can only read takes no events.
        var open = (calendars.Value ?? []).Where(c => c.ArchivedAt is null && !c.IsReadOnly).ToList();
        if (open.Count == 0)
            return AssistantCommandOutcome.Failed(context.Text(
                "Você ainda não tem nenhum calendário. Crie um na Agenda primeiro.",
                "You don't have a calendar yet. Create one in the Agenda first."));
        var (calendar, problem) = AgendaToolArguments.PickNamedOrDefault(
            context, arguments, "calendar", open, c => c.Name, c => c.IsDefault, "seus calendários", "your calendars");
        if (calendar is null)
            return AssistantCommandOutcome.Failed(problem!);

        var result = await sender.Send(new CreateEventCommand(new CreateEventInput(
            context.UserId, calendar.Id, e.Title, e.Description, e.Location, Url: null,
            e.StartsAt, e.EndsAt, e.IsAllDay, TimeZone: null, e.Rrule, Status: null)), ct);

        if (!result.IsSuccess)
            return AssistantCommandOutcome.Failed(string.Join("; ", result.Errors.Select(x => x.Message)));

        var when = FormatDayOrInstant(context, e.StartsAt, !e.IsAllDay);
        var repeats = AgendaToolArguments.Repeats(context, e.Rrule);
        return AssistantCommandOutcome.Ok(context.Text(
            $"Evento \"{result.Value!.Title}\" criado em {when}{repeats} ({calendar.Name}).",
            $"Event \"{result.Value!.Title}\" created on {when}{repeats} ({calendar.Name})."));
    }

    private sealed record Args(
        string Title, DateTimeOffset StartsAt, DateTimeOffset EndsAt, bool IsAllDay, string? Location, string? Description,
        string? Rrule);

    private static Args Parse(AssistantToolContext context, JsonElement arguments)
    {
        var title = RequiredString(arguments, "title");
        var (startsAt, hasTime) = OptionalDayOrInstant(context, arguments, "startsAt")
            ?? throw new ArgumentException("The 'startsAt' argument is required.");
        var endsAt = OptionalDayOrInstant(context, arguments, "endsAt")?.At
            ?? (hasTime ? startsAt.AddHours(1) : startsAt.AddDays(1));
        return new Args(title, startsAt, endsAt, !hasTime, OptionalString(arguments, "location"),
            OptionalString(arguments, "description"), AgendaToolArguments.Recurrence(arguments));
    }
}
