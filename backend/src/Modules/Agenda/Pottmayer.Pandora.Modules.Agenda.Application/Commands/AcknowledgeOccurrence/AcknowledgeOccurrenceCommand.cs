using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Security.Authorization.Abstractions;
using Pottmayer.Pandora.Modules.Agenda.Abstractions;

namespace Pottmayer.Pandora.Modules.Agenda.Application.Commands.AcknowledgeOccurrence;

public sealed record AcknowledgeOccurrenceInput(Guid UserId, Guid ReminderId, DateTimeOffset OccurrenceStartsAt);

/// <summary>Acknowledges one occurrence of a recurring reminder. The series is untouched.</summary>
[RequiresPermission(AgendaPermissions.Events.Update)]
public sealed class AcknowledgeOccurrenceCommand(AcknowledgeOccurrenceInput input)
    : CommandBase<AcknowledgeOccurrenceInput, bool>(input);
