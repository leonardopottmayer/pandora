using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Security.Authorization.Abstractions;
using Pottmayer.Pandora.Modules.Agenda.Abstractions;

namespace Pottmayer.Pandora.Modules.Agenda.Application.Commands.AcknowledgeReminder;

public sealed record AcknowledgeReminderInput(Guid UserId, Guid ReminderId);

[RequiresPermission(AgendaPermissions.Reminders.Update)]
public sealed class AcknowledgeReminderCommand(AcknowledgeReminderInput input)
    : CommandBase<AcknowledgeReminderInput, bool>(input);
