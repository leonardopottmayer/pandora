using System.Globalization;
using System.Text.Json;
using Pottmayer.Pandora.Modules.Agenda.Application.Commands.CreateReminder;
using Pottmayer.Pandora.Modules.Assistant.Abstractions.Commands;
using Pottmayer.Tars.Core.Mediator.Abstractions;
using Pottmayer.Pandora.Modules.Agenda.Abstractions;

namespace Pottmayer.Pandora.Modules.Agenda.Application.Assistant;

/// <summary>
/// The Agenda's contribution to the assistant catalog: the <c>create_reminder</c> tool. Thin by design —
/// it parses the model's arguments and forwards them to the existing <see cref="CreateReminderCommand"/>
/// through the mediator, duplicating none of its rules. The reminder's own use case resolves the time
/// zone and validates the request; this tool only shapes the call and the reply.
/// </summary>
public sealed class CreateReminderTool(ISender sender) : IAssistantTool
{
    public IReadOnlyList<string> Permissions { get; } = [AgendaPermissions.Reminders.Create];

    public AssistantCommandDescriptor Descriptor { get; } = new(
        Name: "create_reminder",
        Description: "Creates a reminder with a title and a time, once or repeating. Use when the user asks to be reminded of something.",
        ParametersJsonSchema: $$"""
        {
          "type": "object",
          "properties": {
            "title": { "type": "string", "description": "What to be reminded of, in a few words." },
            "remindAt": { "type": "string", "description": "When to remind, as an absolute ISO-8601 timestamp with offset (e.g. 2026-09-05T10:00:00-03:00). For a repeating reminder, the first time." },
            {{AgendaToolArguments.RecurrenceSchema}}
          },
          "required": ["title", "remindAt"]
        }
        """,
        Confirmation: ConfirmationPolicy.WhenAmbiguous,
        Examples:
        [
            new AssistantCommandExample(
                "remind me to call the dentist tomorrow at 9",
                """{ "title": "Call the dentist", "remindAt": "2026-09-05T09:00:00-03:00" }"""),
            new AssistantCommandExample(
                "reminder to pay the rent on the 5th at 10am",
                """{ "title": "Pay the rent", "remindAt": "2026-09-05T10:00:00-03:00" }"""),
            new AssistantCommandExample(
                "remind me to take the medicine every day at 8am",
                """{ "title": "Take the medicine", "remindAt": "2026-09-05T08:00:00-03:00", "recurrence": "FREQ=DAILY" }"""),
        ]);

    public string Describe(AssistantToolContext context, JsonElement arguments)
    {
        var (title, remindAt) = Parse(arguments);
        var when = context.FormatDateTime(remindAt);
        var repeats = AgendaToolArguments.Repeats(context, AgendaToolArguments.Recurrence(arguments));
        return context.Text(
            $"Criar o lembrete \"{title}\" para {when}{repeats}?",
            $"Create the reminder \"{title}\" for {when}{repeats}?");
    }

    public async Task<AssistantCommandOutcome> ExecuteAsync(AssistantToolContext context, JsonElement arguments, CancellationToken ct = default)
    {
        var (title, remindAt) = Parse(arguments);
        var rrule = AgendaToolArguments.Recurrence(arguments);

        var command = new CreateReminderCommand(new CreateReminderInput(
            context.UserId, title, Notes: null, remindAt, TimeZone: null, rrule));
        var result = await sender.Send(command, ct);

        if (!result.IsSuccess)
            return AssistantCommandOutcome.Failed(string.Join("; ", result.Errors.Select(e => e.Message)));

        var dto = result.Value!;
        var when = context.FormatDateTime(dto.RemindAt);
        var repeats = AgendaToolArguments.Repeats(context, rrule);
        return AssistantCommandOutcome.Ok(context.Text(
            $"Lembrete \"{dto.Title}\" criado para {when}{repeats}.",
            $"Reminder \"{dto.Title}\" created for {when}{repeats}."));
    }

    private static (string Title, DateTimeOffset RemindAt) Parse(JsonElement arguments)
    {
        if (!arguments.TryGetProperty("title", out var titleElement) || titleElement.ValueKind != JsonValueKind.String)
            throw new ArgumentException("The 'title' argument is required.");

        if (!arguments.TryGetProperty("remindAt", out var remindAtElement) || remindAtElement.ValueKind != JsonValueKind.String)
            throw new ArgumentException("The 'remindAt' argument is required.");

        var remindAt = DateTimeOffset.Parse(
            remindAtElement.GetString()!, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        return (titleElement.GetString()!, remindAt);
    }
}
