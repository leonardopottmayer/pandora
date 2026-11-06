using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Security.Authorization.Abstractions;
using Pottmayer.Pandora.Modules.Agenda.Abstractions;

namespace Pottmayer.Pandora.Modules.Agenda.Application.Commands.DeleteCalendar;

public sealed record DeleteCalendarInput(Guid UserId, Guid CalendarId);

/// <summary>Deletes an empty calendar. Refused while it still has live events — archive instead.</summary>
[RequiresPermission(AgendaPermissions.Calendars.Delete)]
public sealed class DeleteCalendarCommand(DeleteCalendarInput input)
    : CommandBase<DeleteCalendarInput, bool>(input);
