namespace Pottmayer.Pandora.Modules.Agenda.Application.Dtos;

/// <summary>A reminder as shown on the list and returned after a change.</summary>
public sealed record ReminderDto(
    Guid Id,
    string Title,
    string? Notes,
    DateTimeOffset RemindAt,
    string TimeZone,
    string? Rrule,
    DateTimeOffset? RecurrenceEndsAt,
    string Status,
    DateTimeOffset? SnoozedUntil,
    DateTimeOffset? AcknowledgedAt);
