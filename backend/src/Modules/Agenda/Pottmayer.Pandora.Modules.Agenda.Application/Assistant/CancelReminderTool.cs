using System.Text.Json;
using Pottmayer.Pandora.Modules.Agenda.Application.Commands.CancelReminder;
using Pottmayer.Pandora.Modules.Assistant.Abstractions.Commands;
using Pottmayer.Tars.Core.Mediator.Abstractions;
using static Pottmayer.Pandora.Modules.Assistant.Abstractions.Commands.ToolArguments;

namespace Pottmayer.Pandora.Modules.Agenda.Application.Assistant;

/// <summary>
/// <c>cancel_reminder</c>: stops a pending reminder for good — a repeating one, every occurrence
/// (<see cref="CancelReminderCommand"/>, the web's delete). Always confirmed first, whatever the user's level.
/// </summary>
public sealed class CancelReminderTool(ISender sender) : IAssistantTargetedTool
{
    public AssistantCommandDescriptor Descriptor { get; } = new(
        Name: "cancel_reminder",
        Description: "Cancels (deletes) one of the user's pending reminders. You cannot see the reminders: point at it by its number on the last list, or pass the words the user used to name it.",
        ParametersJsonSchema: """
        {
          "type": "object",
          "properties": {
            "ref": { "type": "integer", "description": "The reminder's number on the last list shown, when the user points at it by number." },
            "reminder": { "type": "string", "description": "The reminder as the user named it (e.g. \"aluguel\"), when not pointing by number." }
          }
        }
        """,
        Confirmation: ConfirmationPolicy.Required,
        Examples:
        [
            new AssistantCommandExample("cancel the rent reminder", """{ "reminder": "rent" }"""),
            new AssistantCommandExample("delete 2", """{ "ref": 2 }"""),
        ]);

    public string Describe(AssistantToolContext context, JsonElement arguments)
    {
        var name = TargetName(arguments, "reminder");
        return context.Text($"Cancelar o lembrete \"{name}\"?", $"Cancel the reminder \"{name}\"?");
    }

    public async Task<AssistantCommandOutcome> ExecuteAsync(AssistantToolContext context, JsonElement arguments, CancellationToken ct = default)
    {
        var (reminder, problem) = await ReminderTarget.FindAsync(sender, context, arguments, ct);
        if (reminder is null)
            return AssistantCommandOutcome.Failed(problem!);

        var result = await sender.Send(new CancelReminderCommand(new CancelReminderInput(context.UserId, reminder.Id)), ct);
        if (!result.IsSuccess)
            return AssistantCommandOutcome.Failed(string.Join("; ", result.Errors.Select(e => e.Message)));

        return AssistantCommandOutcome.Ok(reminder.Rrule is null
            ? context.Text($"Lembrete \"{reminder.Title}\" cancelado.", $"Reminder \"{reminder.Title}\" cancelled.")
            : context.Text($"Lembrete \"{reminder.Title}\" cancelado (todas as repetições).", $"Reminder \"{reminder.Title}\" cancelled (every repetition)."));
    }

    public async Task<(ListedItem? Target, string? Problem)> FindTargetAsync(
        AssistantToolContext context, JsonElement arguments, CancellationToken ct = default) =>
        ReminderTarget.AsTarget(await ReminderTarget.FindAsync(sender, context, arguments, ct));
}
