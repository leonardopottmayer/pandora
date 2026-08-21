using Pottmayer.Pandora.Modules.Agenda.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Queries;

namespace Pottmayer.Pandora.Modules.Agenda.Application.Queries.GetCalendars;

public sealed record GetCalendarsInput(Guid UserId);

public sealed class GetCalendarsQuery(GetCalendarsInput input)
    : QueryBase<GetCalendarsInput, IReadOnlyList<CalendarDto>>(input);
