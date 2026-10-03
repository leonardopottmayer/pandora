using System.Text.Json;
using Pottmayer.Pandora.Modules.Agenda.Application.Commands.RenameReminder;
using Pottmayer.Pandora.Modules.Assistant.Abstractions.Commands;
using Pottmayer.Tars.Core.Mediator.Abstractions;
using static Pottmayer.Pandora.Modules.Assistant.Abstractions.Commands.ToolArguments;

namespace Pottmayer.Pandora.Modules.Agenda.Application.Assistant;

/// <summary>
/// <c>rename_reminder</c>: gives a pending reminder a new title (<see cref="RenameReminderCommand"/>). Moving
/// it in time is <c>reschedule_reminder</c>.
/// </summary>
public sealed class RenameReminderTool(ISender sender) : IAssistantTargetedTool
{
    public AssistantCommandDescriptor Descriptor { get; } = new(
        Name: "rename_reminder",
        Description: "Renames one of the user's pending reminders. You cannot see the reminders: point at it by its number on the last list, or pass the words the user used to name it. To change its time use reschedule_reminder.",
        ParametersJsonSchema: """
        {
          "type": "object",
          "properties": {
            "ref": { "type": "integer", "description": "The reminder's number on the last list shown, when the user points at it by number." },
            "reminder": { "type": "string", "description": "The reminder as the user named it (e.g. \"aluguel\"), when not pointing by number." },
            "title": { "type": "string", "description": "The new title, in a few words." }
          },
          "required": ["title"]
        }
        """,
        Confirmation: ConfirmationPolicy.WhenAmbiguous,
        Examples:
        [
            new AssistantCommandExample("rename the rent reminder to pay rent and condo fee",
                """{ "reminder": "rent", "title": "Pay rent and condo fee" }"""),
            new AssistantCommandExample("call 2 \"take the medicine after lunch\"",
                """{ "ref": 2, "title": "Take the medicine after lunch" }"""),
        ]);

    public string Describe(AssistantToolContext context, JsonElement arguments)
    {
        var name = TargetName(arguments, "reminder");
        var title = RequiredString(arguments, "title");
        return context.Text($"Renomear o lembrete \"{name}\" para \"{title}\"?", $"Rename the reminder \"{name}\" to \"{title}\"?");
    }

    public async Task<AssistantCommandOutcome> ExecuteAsync(AssistantToolContext context, JsonElement arguments, CancellationToken ct = default)
    {
        var title = RequiredString(arguments, "title");

        var (reminder, problem) = await ReminderTarget.FindAsync(sender, context, arguments, ct);
        if (reminder is null)
            return AssistantCommandOutcome.Failed(problem!);

        var result = await sender.Send(new RenameReminderCommand(new RenameReminderInput(context.UserId, reminder.Id, title)), ct);
        if (!result.IsSuccess)
            return AssistantCommandOutcome.Failed(string.Join("; ", result.Errors.Select(e => e.Message)));

        return AssistantCommandOutcome.Ok(context.Text(
            $"Lembrete \"{reminder.Title}\" renomeado para \"{title}\".",
            $"Reminder \"{reminder.Title}\" renamed to \"{title}\"."));
    }

    public async Task<(ListedItem? Target, string? Problem)> FindTargetAsync(
        AssistantToolContext context, JsonElement arguments, CancellationToken ct = default) =>
        ReminderTarget.AsTarget(await ReminderTarget.FindAsync(sender, context, arguments, ct));
}
