namespace Pottmayer.Pandora.Modules.Agenda.Application.Dtos;

/// <summary>
/// One entry in the unified day view (<c>GET /agenda/today</c>): an expanded event occurrence, a task
/// due today, or a reminder firing today — merged and ordered by <see cref="At"/>.
/// </summary>
public sealed record TodayItemDto(
    string Kind,
    Guid Id,
    string Title,
    string? Notes,
    DateTimeOffset At,
    DateTimeOffset? EndsAt,
    bool IsAllDay,
    Guid? CalendarId,
    string? Status);
