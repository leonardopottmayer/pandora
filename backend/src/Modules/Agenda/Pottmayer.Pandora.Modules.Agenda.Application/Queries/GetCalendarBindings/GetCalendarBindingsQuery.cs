using Pottmayer.Pandora.Modules.Agenda.Abstractions;
using Pottmayer.Pandora.Modules.Agenda.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Security.Authorization.Abstractions;

namespace Pottmayer.Pandora.Modules.Agenda.Application.Queries.GetCalendarBindings;

public sealed record GetCalendarBindingsInput(Guid UserId);

/// <summary>The user's Google calendar links, with their sync status.</summary>
[RequiresPermission(AgendaPermissions.Calendars.Read)]
public sealed class GetCalendarBindingsQuery(GetCalendarBindingsInput input)
    : QueryBase<GetCalendarBindingsInput, IReadOnlyList<CalendarBindingDto>>(input);
