using Pottmayer.Tars.Core.Cqrs.Commands;

namespace Pottmayer.Pandora.Modules.Agenda.Application.Commands.CancelReminder;

public sealed record CancelReminderInput(Guid UserId, Guid ReminderId);

public sealed class CancelReminderCommand(CancelReminderInput input)
    : CommandBase<CancelReminderInput, bool>(input);
