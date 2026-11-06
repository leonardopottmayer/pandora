using Pottmayer.Pandora.Modules.Agenda.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Security.Authorization.Abstractions;
using Pottmayer.Pandora.Modules.Agenda.Abstractions;

namespace Pottmayer.Pandora.Modules.Agenda.Application.Commands.CreateEvent;

/// <summary>
/// Creates an event. <see cref="Rrule"/> set ⇒ a recurring series anchored at <see cref="StartsAt"/>;
/// null ⇒ a single occurrence. <see cref="TimeZone"/> null ⇒ UTC (per-user zone deferred).
/// </summary>
public sealed record CreateEventInput(
    Guid UserId,
    Guid CalendarId,
    string Title,
    string? Description,
    string? Location,
    string? Url,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    bool IsAllDay,
    string? TimeZone,
    string? Rrule,
    string? Status);

[RequiresPermission(AgendaPermissions.Events.Create)]
public sealed class CreateEventCommand(CreateEventInput input)
    : CommandBase<CreateEventInput, EventDto>(input);
