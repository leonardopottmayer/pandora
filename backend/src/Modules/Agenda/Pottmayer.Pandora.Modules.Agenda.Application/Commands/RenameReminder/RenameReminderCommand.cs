using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Security.Authorization.Abstractions;
using Pottmayer.Pandora.Modules.Agenda.Abstractions;

namespace Pottmayer.Pandora.Modules.Agenda.Application.Commands.RenameReminder;

public sealed record RenameReminderInput(Guid UserId, Guid ReminderId, string Title);

/// <summary>Changes a reminder's title; its time, recurrence and status are untouched.</summary>
[RequiresPermission(AgendaPermissions.Reminders.Update)]
public sealed class RenameReminderCommand(RenameReminderInput input)
    : CommandBase<RenameReminderInput, bool>(input);
