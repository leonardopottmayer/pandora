using Pottmayer.Pandora.Modules.Agenda.Abstractions;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Security.Authorization.Abstractions;

namespace Pottmayer.Pandora.Modules.Agenda.Application.Commands.UnlinkCalendar;

public sealed record UnlinkCalendarInput(Guid UserId, Guid BindingId);

/// <summary>
/// Stops syncing a Google calendar. The Pandora calendar and its events stay, as an ordinary local
/// calendar the user can edit; nothing is deleted in Google.
/// </summary>
[RequiresPermission(AgendaPermissions.Calendars.Delete)]
public sealed class UnlinkCalendarCommand(UnlinkCalendarInput input)
    : CommandBase<UnlinkCalendarInput, bool>(input);
