using Pottmayer.Tars.Core.Cqrs.Commands;

namespace Pottmayer.Pandora.Modules.Agenda.Application.Commands.SnoozeReminder;

public sealed record SnoozeReminderInput(Guid UserId, Guid ReminderId, DateTimeOffset SnoozedUntil);

public sealed class SnoozeReminderCommand(SnoozeReminderInput input)
    : CommandBase<SnoozeReminderInput, bool>(input);
