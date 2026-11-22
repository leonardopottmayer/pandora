using Pottmayer.Pandora.Modules.Agenda.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Security.Authorization.Abstractions;
using Pottmayer.Pandora.Modules.Agenda.Abstractions;

namespace Pottmayer.Pandora.Modules.Agenda.Application.Commands.CreateCalendar;

/// <summary>Creates a local calendar. <see cref="TimeZone"/> null ⇒ UTC (per-user zone deferred).</summary>
public sealed record CreateCalendarInput(
    Guid UserId, string Name, string? Color, bool IsDefault, string? TimeZone);

[RequiresPermission(AgendaPermissions.Calendars.Create)]
public sealed class CreateCalendarCommand(CreateCalendarInput input)
    : CommandBase<CreateCalendarInput, CalendarDto>(input);
