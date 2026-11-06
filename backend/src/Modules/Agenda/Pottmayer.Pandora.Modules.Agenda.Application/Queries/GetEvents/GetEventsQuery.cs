using Pottmayer.Pandora.Modules.Agenda.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Security.Authorization.Abstractions;
using Pottmayer.Pandora.Modules.Agenda.Abstractions;

namespace Pottmayer.Pandora.Modules.Agenda.Application.Queries.GetEvents;

/// <summary>Expanded occurrences overlapping <c>[From, To]</c>, optionally limited to some calendars.</summary>
public sealed record GetEventsInput(
    Guid UserId, DateTimeOffset From, DateTimeOffset To, IReadOnlyList<Guid>? CalendarIds);

[RequiresPermission(AgendaPermissions.Events.Read)]
public sealed class GetEventsQuery(GetEventsInput input)
    : QueryBase<GetEventsInput, IReadOnlyList<EventOccurrenceDto>>(input);
