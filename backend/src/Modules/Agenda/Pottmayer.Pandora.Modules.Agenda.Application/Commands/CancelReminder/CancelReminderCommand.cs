using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Security.Authorization.Abstractions;
using Pottmayer.Pandora.Modules.Agenda.Abstractions;

namespace Pottmayer.Pandora.Modules.Agenda.Application.Commands.CancelReminder;

public sealed record CancelReminderInput(Guid UserId, Guid ReminderId);

[RequiresPermission(AgendaPermissions.Reminders.Delete)]
public sealed class CancelReminderCommand(CancelReminderInput input)
    : CommandBase<CancelReminderInput, bool>(input);
