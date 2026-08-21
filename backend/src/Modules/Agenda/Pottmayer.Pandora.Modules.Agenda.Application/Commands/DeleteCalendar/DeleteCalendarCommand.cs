using Pottmayer.Tars.Core.Cqrs.Commands;

namespace Pottmayer.Pandora.Modules.Agenda.Application.Commands.DeleteCalendar;

public sealed record DeleteCalendarInput(Guid UserId, Guid CalendarId);

/// <summary>Deletes an empty calendar. Refused while it still has live events — archive instead.</summary>
public sealed class DeleteCalendarCommand(DeleteCalendarInput input)
    : CommandBase<DeleteCalendarInput, bool>(input);
