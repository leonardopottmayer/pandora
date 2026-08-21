using Pottmayer.Pandora.Modules.Agenda.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Queries;

namespace Pottmayer.Pandora.Modules.Agenda.Application.Queries.GetEvents;

/// <summary>Expanded occurrences overlapping <c>[From, To]</c>, optionally limited to some calendars.</summary>
public sealed record GetEventsInput(
    Guid UserId, DateTimeOffset From, DateTimeOffset To, IReadOnlyList<Guid>? CalendarIds);

public sealed class GetEventsQuery(GetEventsInput input)
    : QueryBase<GetEventsInput, IReadOnlyList<EventOccurrenceDto>>(input);
