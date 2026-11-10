using Pottmayer.Pandora.Modules.Agenda.Abstractions;
using Pottmayer.Pandora.Modules.Agenda.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Security.Authorization.Abstractions;

namespace Pottmayer.Pandora.Modules.Agenda.Application.Commands.BindGoogleCalendars;

/// <summary>The Google calendars of <see cref="AccountId"/> to bring into Pandora, each as its own calendar.</summary>
public sealed record BindGoogleCalendarsInput(Guid UserId, Guid AccountId, IReadOnlyList<string> RemoteCalendarIds);

/// <summary>
/// Creates one Pandora calendar per Google calendar (named and colored like it, in its zone) and the
/// binding that feeds it. A calendar already bound is left as it is. The events arrive on the first sync.
/// </summary>
[RequiresPermission(AgendaPermissions.Calendars.Create)]
public sealed class BindGoogleCalendarsCommand(BindGoogleCalendarsInput input)
    : CommandBase<BindGoogleCalendarsInput, IReadOnlyList<CalendarBindingDto>>(input);
