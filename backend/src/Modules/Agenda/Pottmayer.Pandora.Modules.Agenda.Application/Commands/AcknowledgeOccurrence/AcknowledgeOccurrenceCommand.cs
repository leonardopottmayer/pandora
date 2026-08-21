using Pottmayer.Tars.Core.Cqrs.Commands;

namespace Pottmayer.Pandora.Modules.Agenda.Application.Commands.AcknowledgeOccurrence;

public sealed record AcknowledgeOccurrenceInput(Guid UserId, Guid ReminderId, DateTimeOffset OccurrenceStartsAt);

/// <summary>Acknowledges one occurrence of a recurring reminder. The series is untouched.</summary>
public sealed class AcknowledgeOccurrenceCommand(AcknowledgeOccurrenceInput input)
    : CommandBase<AcknowledgeOccurrenceInput, bool>(input);
