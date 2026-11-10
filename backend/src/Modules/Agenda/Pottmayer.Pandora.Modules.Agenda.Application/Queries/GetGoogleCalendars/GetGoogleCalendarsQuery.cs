using Pottmayer.Pandora.Modules.Agenda.Abstractions;
using Pottmayer.Pandora.Modules.Agenda.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Security.Authorization.Abstractions;

namespace Pottmayer.Pandora.Modules.Agenda.Application.Queries.GetGoogleCalendars;

public sealed record GetGoogleCalendarsInput(Guid UserId, Guid AccountId);

/// <summary>The calendars a connected Google account can see, marking those already bound.</summary>
[RequiresPermission(AgendaPermissions.Calendars.Read)]
public sealed class GetGoogleCalendarsQuery(GetGoogleCalendarsInput input)
    : QueryBase<GetGoogleCalendarsInput, IReadOnlyList<GoogleCalendarDto>>(input);
