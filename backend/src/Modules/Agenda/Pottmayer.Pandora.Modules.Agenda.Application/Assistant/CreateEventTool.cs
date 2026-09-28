using System.Text.Json;
using Pottmayer.Pandora.Modules.Agenda.Application.Commands.CreateEvent;
using Pottmayer.Pandora.Modules.Agenda.Application.Queries.GetCalendars;
using Pottmayer.Pandora.Modules.Assistant.Abstractions.Commands;
using Pottmayer.Tars.Core.Mediator.Abstractions;
using static Pottmayer.Pandora.Modules.Agenda.Application.Assistant.ToolArguments;

namespace Pottmayer.Pandora.Modules.Agenda.Application.Assistant;

/// <summary>
/// <c>create_event</c>: a single (non-recurring) event in the user's default calendar (the first open one
/// when none is marked default), forwarded to <see cref="CreateEventCommand"/>. Without an end it lasts an
/// hour; a bare start date makes it an all-day event.
/// </summary>
public sealed class CreateEventTool(ISender sender) : IAssistantTool
{
    public AssistantCommandDescriptor Descriptor { get; } = new(
        Name: "create_event",
        Description: "Creates a calendar event (an appointment, meeting or something happening at a time or on a day). Use when the user schedules something, not when they want to be pinged (create_reminder) or note a to-do (create_task).",
        ParametersJsonSchema: """
        {
          "type": "object",
          "properties": {
            "title": { "type": "string", "description": "What the event is, in a few words." },
            "startsAt": { "type": "string", "description": "When it starts: an ISO-8601 timestamp with offset, or a bare date (2026-09-05) for an all-day event." },
            "endsAt": { "type": "string", "description": "When it ends, same format as startsAt. Omit when the user did not say." },
            "location": { "type": "string", "description": "Where, only when the user said." }
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
        ]);

    public string Describe(AssistantToolContext context, JsonElement arguments)
    {
        var e = Parse(context, arguments);
        var when = FormatDayOrInstant(context, e.StartsAt, !e.IsAllDay);
        return context.Text($"Criar o evento \"{e.Title}\" em {when}?", $"Create the event \"{e.Title}\" on {when}?");
    }

    public async Task<AssistantCommandOutcome> ExecuteAsync(AssistantToolContext context, JsonElement arguments, CancellationToken ct = default)
    {
        var e = Parse(context, arguments);

        var calendars = await sender.Send(new GetCalendarsQuery(new GetCalendarsInput(context.UserId)), ct);
        var open = (calendars.Value ?? []).Where(c => c.ArchivedAt is null).ToList();
        var calendar = open.FirstOrDefault(c => c.IsDefault) ?? open.FirstOrDefault();
        if (calendar is null)
            return AssistantCommandOutcome.Failed(context.Text(
                "Você ainda não tem nenhum calendário. Crie um na Agenda primeiro.",
                "You don't have a calendar yet. Create one in the Agenda first."));

        var result = await sender.Send(new CreateEventCommand(new CreateEventInput(
            context.UserId, calendar.Id, e.Title, Description: null, e.Location, Url: null,
            e.StartsAt, e.EndsAt, e.IsAllDay, TimeZone: null, Rrule: null, Status: null)), ct);

        if (!result.IsSuccess)
            return AssistantCommandOutcome.Failed(string.Join("; ", result.Errors.Select(x => x.Message)));

        var when = FormatDayOrInstant(context, e.StartsAt, !e.IsAllDay);
        return AssistantCommandOutcome.Ok(context.Text(
            $"Evento \"{result.Value!.Title}\" criado em {when} ({calendar.Name}).",
            $"Event \"{result.Value!.Title}\" created on {when} ({calendar.Name})."));
    }

    private sealed record Args(string Title, DateTimeOffset StartsAt, DateTimeOffset EndsAt, bool IsAllDay, string? Location);

    private static Args Parse(AssistantToolContext context, JsonElement arguments)
    {
        var title = RequiredString(arguments, "title");
        var (startsAt, hasTime) = OptionalDayOrInstant(context, arguments, "startsAt")
            ?? throw new ArgumentException("The 'startsAt' argument is required.");
        var endsAt = OptionalDayOrInstant(context, arguments, "endsAt")?.At
            ?? (hasTime ? startsAt.AddHours(1) : startsAt.AddDays(1));
        return new Args(title, startsAt, endsAt, !hasTime, OptionalString(arguments, "location"));
    }
}
