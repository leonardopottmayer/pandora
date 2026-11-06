using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Security.Authorization.Abstractions;
using Pottmayer.Pandora.Modules.Agenda.Abstractions;

namespace Pottmayer.Pandora.Modules.Agenda.Application.Commands.RescheduleReminder;

public sealed record RescheduleReminderInput(Guid UserId, Guid ReminderId, DateTimeOffset At);

/// <summary>
/// Moves a single-shot reminder to a new time and re-arms it (a recurring one is refused). Snoozing, by
/// contrast, defers an alert that already fired and keeps the remind time.
/// </summary>
[RequiresPermission(AgendaPermissions.Reminders.Update)]
public sealed class RescheduleReminderCommand(RescheduleReminderInput input)
    : CommandBase<RescheduleReminderInput, bool>(input);
