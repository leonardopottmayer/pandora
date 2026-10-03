using System.Text.Json;
using Pottmayer.Pandora.Modules.Agenda.Application.Commands.RescheduleReminder;
using Pottmayer.Pandora.Modules.Assistant.Abstractions.Commands;
using Pottmayer.Tars.Core.Mediator.Abstractions;
using static Pottmayer.Pandora.Modules.Assistant.Abstractions.Commands.ToolArguments;

namespace Pottmayer.Pandora.Modules.Agenda.Application.Assistant;

/// <summary>
/// <c>reschedule_reminder</c>: moves a pending one-off reminder to another time — earlier or later — and
/// re-arms it (<see cref="RescheduleReminderCommand"/>), so every view shows the new time. Like
/// <see cref="CompleteTaskTool"/>, the reminder is found here by the user's words or its number on a list,
/// never shown to the model.
/// </summary>
public sealed class RescheduleReminderTool(ISender sender) : IAssistantTargetedTool
{
    public AssistantCommandDescriptor Descriptor { get; } = new(
        Name: "reschedule_reminder",
        Description: "Moves one of the user's pending reminders to another time (reschedule, postpone, snooze, bring forward). You cannot see the reminders: point at it by its number on the last list, or pass the words the user used to name it; the system finds it.",
        ParametersJsonSchema: """
        {
          "type": "object",
          "properties": {
            "ref": { "type": "integer", "description": "The reminder's number on the last list shown, when the user points at it by number." },
            "reminder": { "type": "string", "description": "The reminder as the user named it (e.g. \"aluguel\", \"dentist\"), when not pointing by number." },
            "at": { "type": "string", "description": "The new time, as an absolute ISO-8601 timestamp with offset (resolve \"in 1 hour\", \"tomorrow\" against Now)." }
          },
          "required": ["at"]
        }
        """,
        Confirmation: ConfirmationPolicy.WhenAmbiguous,
        Examples:
        [
            new AssistantCommandExample(
                "snooze the dentist reminder for an hour",
                """{ "reminder": "dentist", "at": "2026-09-04T11:00:00-03:00" }"""),
            new AssistantCommandExample(
                "move the rent reminder to tomorrow at 9",
                """{ "reminder": "rent", "at": "2026-09-05T09:00:00-03:00" }"""),
        ]);

    public string Describe(AssistantToolContext context, JsonElement arguments)
    {
        var name = TargetName(arguments, "reminder");
        var when = context.FormatDateTime(RequiredInstant(arguments, "at"));
        return context.Text($"Remarcar o lembrete \"{name}\" para {when}?", $"Move the reminder \"{name}\" to {when}?");
    }

    public async Task<AssistantCommandOutcome> ExecuteAsync(AssistantToolContext context, JsonElement arguments, CancellationToken ct = default)
    {
        var at = RequiredInstant(arguments, "at");

        var (reminder, problem) = await ReminderTarget.FindAsync(sender, context, arguments, ct);
        if (reminder is null)
            return AssistantCommandOutcome.Failed(problem!);

        // ponytail: a recurring reminder moves per occurrence (SnoozeOccurrenceCommand), which needs the
        // occurrence that fired; the notification's own button does that. Add it here if asked for by voice.
        if (reminder.Rrule is not null)
            return AssistantCommandOutcome.Failed(context.Text(
                $"\"{reminder.Title}\" é recorrente; adie pelo botão da notificação.",
                $"\"{reminder.Title}\" is recurring; snooze it from the notification's button."));

        var result = await sender.Send(new RescheduleReminderCommand(new RescheduleReminderInput(context.UserId, reminder.Id, at)), ct);
        if (!result.IsSuccess)
            return AssistantCommandOutcome.Failed(string.Join("; ", result.Errors.Select(e => e.Message)));

        var when = context.FormatDateTime(at);
        return AssistantCommandOutcome.Ok(context.Text(
            $"Lembrete \"{reminder.Title}\" remarcado para {when}.",
            $"Reminder \"{reminder.Title}\" moved to {when}."));
    }

    public async Task<(ListedItem? Target, string? Problem)> FindTargetAsync(
        AssistantToolContext context, JsonElement arguments, CancellationToken ct = default) =>
        ReminderTarget.AsTarget(await ReminderTarget.FindAsync(sender, context, arguments, ct));
}
