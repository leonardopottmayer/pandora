using System.Text.Json;
using Pottmayer.Pandora.Modules.Agenda.Application.Dtos;
using Pottmayer.Pandora.Modules.Agenda.Application.Queries.GetReminders;
using Pottmayer.Pandora.Modules.Agenda.Domain.ValueObjects;
using Pottmayer.Pandora.Modules.Assistant.Abstractions.Commands;
using Pottmayer.Tars.Core.Mediator.Abstractions;
using static Pottmayer.Pandora.Modules.Assistant.Abstractions.Commands.ToolArguments;

namespace Pottmayer.Pandora.Modules.Agenda.Application.Assistant;

/// <summary>
/// The pending reminder a call is about, for the reminder tools: the one the user pointed at (<c>ref</c>), or
/// the one whose title matches their words (<c>reminder</c>).
/// </summary>
internal static class ReminderTarget
{
    public static async Task<(ReminderDto? Match, string? Problem)> FindAsync(
        ISender sender, AssistantToolContext context, JsonElement arguments, CancellationToken ct)
    {
        var reminders = await sender.Send(new GetRemindersQuery(new GetRemindersInput(context.UserId)), ct);
        var pending = (reminders.Value ?? []).Where(r =>
            r.Status is not (nameof(ReminderStatus.Acknowledged) or nameof(ReminderStatus.Cancelled)));
        return PickTarget(context, arguments, "reminder", "reminder", pending,
            r => r.Id, r => r.Title, "seus lembretes pendentes", "your pending reminders");
    }

    /// <summary>A found reminder as the item a confirmation pins (<see cref="IAssistantTargetedTool"/>).</summary>
    public static (ListedItem? Target, string? Problem) AsTarget((ReminderDto? Match, string? Problem) found) =>
        found.Match is { } r ? (new ListedItem("reminder", r.Id, r.Title), null) : (null, found.Problem);
}
