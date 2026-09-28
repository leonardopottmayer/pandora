using System.Text.Json;
using Pottmayer.Pandora.Modules.Agenda.Application.Commands.SnoozeReminder;
using Pottmayer.Pandora.Modules.Agenda.Application.Queries.GetReminders;
using Pottmayer.Pandora.Modules.Agenda.Domain.ValueObjects;
using Pottmayer.Pandora.Modules.Assistant.Abstractions.Commands;
using Pottmayer.Tars.Core.Mediator.Abstractions;
using static Pottmayer.Pandora.Modules.Assistant.Abstractions.Commands.ToolArguments;

namespace Pottmayer.Pandora.Modules.Agenda.Application.Assistant;

/// <summary>
/// <c>snooze_reminder</c>: pushes a pending one-off reminder to a later time. Like
/// <see cref="CompleteTaskTool"/>, the reminder is found here by the user's words, never shown to the
/// model. Forwarded to <see cref="SnoozeReminderCommand"/>.
/// </summary>
public sealed class SnoozeReminderTool(ISender sender) : IAssistantTool
{
    public AssistantCommandDescriptor Descriptor { get; } = new(
        Name: "snooze_reminder",
        Description: "Postpones one of the user's pending reminders to a later time. You cannot see the reminders: pass the words the user used to name it; the system finds it.",
        ParametersJsonSchema: """
        {
          "type": "object",
          "properties": {
            "reminder": { "type": "string", "description": "The reminder as the user named it (e.g. \"aluguel\", \"dentist\")." },
            "until": { "type": "string", "description": "The new time, as an absolute ISO-8601 timestamp with offset (resolve \"in 1 hour\", \"tomorrow\" against Now)." }
          },
          "required": ["reminder", "until"]
        }
        """,
        Confirmation: ConfirmationPolicy.WhenAmbiguous,
        Examples:
        [
            new AssistantCommandExample(
                "snooze the dentist reminder for an hour",
                """{ "reminder": "dentist", "until": "2026-09-04T11:00:00-03:00" }"""),
            new AssistantCommandExample(
                "push the rent reminder to tomorrow at 9",
                """{ "reminder": "rent", "until": "2026-09-05T09:00:00-03:00" }"""),
        ]);

    public string Describe(AssistantToolContext context, JsonElement arguments)
    {
        var (query, until) = Parse(arguments);
        var when = context.FormatDateTime(until);
        return context.Text($"Adiar o lembrete \"{query}\" para {when}?", $"Snooze the reminder \"{query}\" until {when}?");
    }

    public async Task<AssistantCommandOutcome> ExecuteAsync(AssistantToolContext context, JsonElement arguments, CancellationToken ct = default)
    {
        var (query, until) = Parse(arguments);

        var reminders = await sender.Send(new GetRemindersQuery(new GetRemindersInput(context.UserId)), ct);
        var pending = (reminders.Value ?? []).Where(r =>
            r.Status is not (nameof(ReminderStatus.Acknowledged) or nameof(ReminderStatus.Cancelled)));

        var (reminder, problem) = PickByTitle(context, pending, r => r.Title, query, "seus lembretes pendentes", "your pending reminders");
        if (reminder is null)
            return AssistantCommandOutcome.Failed(problem!);

        // ponytail: a recurring reminder is snoozed per occurrence (SnoozeOccurrenceCommand), which needs the
        // occurrence that fired; the notification's own button does that. Add it here if asked for by voice.
        if (reminder.Rrule is not null)
            return AssistantCommandOutcome.Failed(context.Text(
                $"\"{reminder.Title}\" é recorrente; adie pelo botão da notificação.",
                $"\"{reminder.Title}\" is recurring; snooze it from the notification's button."));

        var result = await sender.Send(new SnoozeReminderCommand(new SnoozeReminderInput(context.UserId, reminder.Id, until)), ct);
        if (!result.IsSuccess)
            return AssistantCommandOutcome.Failed(string.Join("; ", result.Errors.Select(e => e.Message)));

        var when = context.FormatDateTime(until);
        return AssistantCommandOutcome.Ok(context.Text(
            $"Lembrete \"{reminder.Title}\" adiado para {when}.",
            $"Reminder \"{reminder.Title}\" snoozed until {when}."));
    }

    private static (string Query, DateTimeOffset Until) Parse(JsonElement arguments) =>
        (RequiredString(arguments, "reminder"), RequiredInstant(arguments, "until"));
}
