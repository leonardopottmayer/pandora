using Pottmayer.Tars.Core.Cqrs.Commands;

namespace Pottmayer.Pandora.Modules.Agenda.Application.Commands.DeleteEvent;

/// <summary>
/// Deletes an event within a <see cref="Scope"/> (doc §5.4): <c>This</c> cancels one occurrence
/// (EXDATE), <c>ThisAndFuture</c> truncates the series before the cut, <c>All</c> soft-deletes the row.
/// <see cref="OccurrenceStart"/> is required for <c>This</c> and <c>ThisAndFuture</c>.
/// </summary>
public sealed record DeleteEventInput(
    Guid UserId, Guid EventId, EventEditScope Scope, DateTimeOffset? OccurrenceStart);

public sealed class DeleteEventCommand(DeleteEventInput input)
    : CommandBase<DeleteEventInput, bool>(input);
