using Pottmayer.Pandora.Modules.Agenda.Domain.ValueObjects;

namespace Pottmayer.Pandora.Modules.Agenda.Domain.Aggregates;

/// <summary>
/// One concrete, resolved occurrence of an <see cref="Event"/> — the series overlaid with any
/// <see cref="EventOccurrenceOverride"/>. This is what a range query returns (occurrences, not rows).
/// <see cref="OriginalStartsAt"/> is the on-grid identity (unchanged by a reschedule);
/// <see cref="StartsAt"/>/<see cref="EndsAt"/> are the effective times.
/// </summary>
public sealed record EventOccurrence(
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
    EventStatus Status);
