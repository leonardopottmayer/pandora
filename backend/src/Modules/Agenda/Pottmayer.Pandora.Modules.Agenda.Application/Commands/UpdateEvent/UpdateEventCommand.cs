using Pottmayer.Pandora.Modules.Agenda.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Commands;

namespace Pottmayer.Pandora.Modules.Agenda.Application.Commands.UpdateEvent;

/// <summary>
/// Edits an event within a <see cref="Scope"/> (doc §5.4):
/// <list type="bullet">
/// <item><c>This</c> — writes a per-occurrence override for <see cref="OccurrenceStart"/>.</item>
/// <item><c>ThisAndFuture</c> — splits the series: the original ends before the cut, a new event carries the tail.</item>
/// <item><c>All</c> — edits the whole series row.</item>
/// </list>
/// <see cref="OccurrenceStart"/> is the occurrence's on-grid original start; it is required for
/// <c>This</c> and <c>ThisAndFuture</c>. Null optional fields fall back to the current value.
/// </summary>
public sealed record UpdateEventInput(
    Guid UserId,
    Guid EventId,
    EventEditScope Scope,
    DateTimeOffset? OccurrenceStart,
    string? Title,
    string? Description,
    string? Location,
    string? Url,
    DateTimeOffset? StartsAt,
    DateTimeOffset? EndsAt,
    bool? IsAllDay,
    Guid? CalendarId);

public sealed class UpdateEventCommand(UpdateEventInput input)
    : CommandBase<UpdateEventInput, EventDto>(input);
