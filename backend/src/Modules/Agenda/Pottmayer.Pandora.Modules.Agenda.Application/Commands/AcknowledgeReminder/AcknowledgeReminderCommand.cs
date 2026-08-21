using Pottmayer.Tars.Core.Cqrs.Commands;

namespace Pottmayer.Pandora.Modules.Agenda.Application.Commands.AcknowledgeReminder;

public sealed record AcknowledgeReminderInput(Guid UserId, Guid ReminderId);

public sealed class AcknowledgeReminderCommand(AcknowledgeReminderInput input)
    : CommandBase<AcknowledgeReminderInput, bool>(input);
