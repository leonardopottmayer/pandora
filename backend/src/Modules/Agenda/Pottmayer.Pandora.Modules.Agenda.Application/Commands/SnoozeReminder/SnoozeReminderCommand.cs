using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Security.Authorization.Abstractions;
using Pottmayer.Pandora.Modules.Agenda.Abstractions;

namespace Pottmayer.Pandora.Modules.Agenda.Application.Commands.SnoozeReminder;

public sealed record SnoozeReminderInput(Guid UserId, Guid ReminderId, DateTimeOffset SnoozedUntil);

[RequiresPermission(AgendaPermissions.Reminders.Update)]
public sealed class SnoozeReminderCommand(SnoozeReminderInput input)
    : CommandBase<SnoozeReminderInput, bool>(input);
