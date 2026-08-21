namespace Pottmayer.Pandora.Modules.Agenda.Application.Dtos;

/// <summary>An event <em>row</em> (the series), returned after a create or edit. Reads return occurrences instead.</summary>
public sealed record EventDto(
    Guid Id,
    Guid CalendarId,
    string Title,
    string? Description,
    string? Location,
    string? Url,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    bool IsAllDay,
    string TimeZone,
    string? Rrule,
    DateTimeOffset? RecurrenceEndsAt,
    string Status);

/// <summary>One expanded occurrence of an event (a series row overlaid with its override).</summary>
public sealed record EventOccurrenceDto(
    Guid EventId,
    Guid CalendarId,
    DateTimeOffset OriginalStartsAt,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    bool IsAllDay,
    string Title,
    string? Description,
    string? Location,
    string? Url,
    string Status);
