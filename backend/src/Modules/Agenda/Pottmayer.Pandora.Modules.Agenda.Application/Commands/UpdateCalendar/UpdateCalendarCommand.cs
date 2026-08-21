using Pottmayer.Pandora.Modules.Agenda.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Commands;

namespace Pottmayer.Pandora.Modules.Agenda.Application.Commands.UpdateCalendar;

/// <summary>Edits a calendar. Null optional fields are left unchanged; <see cref="Archive"/> hides it.</summary>
public sealed record UpdateCalendarInput(
    Guid UserId,
    Guid CalendarId,
    string? Name,
    string? Color,
    bool? IsVisible,
    string? TimeZone,
    bool? IsDefault,
    bool Archive);

public sealed class UpdateCalendarCommand(UpdateCalendarInput input)
    : CommandBase<UpdateCalendarInput, CalendarDto>(input);
