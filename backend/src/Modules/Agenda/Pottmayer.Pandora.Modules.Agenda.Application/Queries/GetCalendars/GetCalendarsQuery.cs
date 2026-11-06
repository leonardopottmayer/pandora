using Pottmayer.Pandora.Modules.Agenda.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Security.Authorization.Abstractions;
using Pottmayer.Pandora.Modules.Agenda.Abstractions;

namespace Pottmayer.Pandora.Modules.Agenda.Application.Queries.GetCalendars;

public sealed record GetCalendarsInput(Guid UserId);

[RequiresPermission(AgendaPermissions.Calendars.Read)]
public sealed class GetCalendarsQuery(GetCalendarsInput input)
    : QueryBase<GetCalendarsInput, IReadOnlyList<CalendarDto>>(input);
