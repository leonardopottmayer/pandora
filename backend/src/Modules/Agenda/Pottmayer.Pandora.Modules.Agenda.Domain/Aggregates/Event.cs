using Pottmayer.Pandora.Modules.Agenda.Domain.Recurrence;
using Pottmayer.Pandora.Modules.Agenda.Domain.ValueObjects;
using Pottmayer.Pandora.Shared.Domain;
using Pottmayer.Tars.Core.Ddd;

namespace Pottmayer.Pandora.Modules.Agenda.Domain.Aggregates;

/// <summary>
/// A calendar event (doc agd002). Unlike a recurring task (materialized one row at a time), an event
/// is <b>calculated, never stored</b>: one row plus an <see cref="Rrule"/>, expanded into occurrences
/// on read (<see cref="EventExpander"/>). Per-occurrence deviations live in
/// <see cref="EventOccurrenceOverride"/> — a cancellation (EXDATE) or an edit of a single occurrence.
///
/// <para>Editing "this and future" splits the series (doc §5.4): the original ends with an
/// <c>UNTIL</c> just before the cut (<see cref="EndSeriesBefore"/>) and a new event carries the tail —
/// the standard iCalendar approach.</para>
/// </summary>
public sealed class Event : AggregateRoot<Guid>, IAuditable
{
    private const int MaxTitleLength = 200;

    public Guid UserId { get; private set; }
    public Guid CalendarId { get; private set; }

    public string Title { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public string? Location { get; private set; }

    /// <summary>The meeting link, if any.</summary>
    public string? Url { get; private set; }

    /// <summary>Series anchor / DTSTART (UTC). For an all-day event, midnight in <see cref="TimeZone"/>.</summary>
    public DateTimeOffset StartsAt { get; private set; }

    /// <summary>End (UTC), exclusive. For an all-day event, midnight in <see cref="TimeZone"/> of the day after the last.</summary>
    public DateTimeOffset EndsAt { get; private set; }

    public bool IsAllDay { get; private set; }

    /// <summary>IANA time zone the recurrence is expanded in. Defaults to UTC until Identity carries a user default.</summary>
    public string TimeZone { get; private set; } = "UTC";

    /// <summary>Raw RRULE (RFC 5545 subset), stored verbatim. Null ⇒ a single occurrence.</summary>
    public string? Rrule { get; private set; }

    /// <summary>Denormalized last-occurrence bound (from UNTIL/COUNT), so a range query can prune by index. Null ⇒ open-ended.</summary>
    public DateTimeOffset? RecurrenceEndsAt { get; private set; }

    public EventStatus Status { get; private set; }

    /// <summary>Soft delete (a future inbound sync can resurrect).</summary>
    public DateTimeOffset? DeletedAt { get; private set; }

    public Guid? CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }

    private Event() { }

    public bool IsRecurring => Rrule is not null;

    /// <summary>The occurrence duration, preserved by every occurrence and by a split.</summary>
    public TimeSpan Duration => EndsAt - StartsAt;

    public static Event Create(
        Guid userId,
        Guid calendarId,
        string title,
        string? description,
        string? location,
        string? url,
        DateTimeOffset startsAt,
        DateTimeOffset endsAt,
        bool isAllDay,
        string timeZone,
        string? rrule,
        EventStatus status,
        TimeProvider timeProvider)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("An event needs a title.", nameof(title));

        startsAt = startsAt.ToUniversalTime();
        endsAt = endsAt.ToUniversalTime();
        if (endsAt < startsAt)
            throw new ArgumentException("An event cannot end before it starts.", nameof(endsAt));

        var zone = string.IsNullOrWhiteSpace(timeZone) ? "UTC" : timeZone;
        var resolvedZone = ResolveZone(zone);

        string? storedRrule = null;
        DateTimeOffset? recurrenceEndsAt = null;
        if (!string.IsNullOrWhiteSpace(rrule))
        {
            // Parse rejects anything outside the supported subset — the write-time guard.
            var rule = RecurrenceRule.Parse(rrule);
            storedRrule = rule.Raw;
            recurrenceEndsAt = rule.ComputeEndsAt(startsAt, resolvedZone);
        }

        return new Event
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            CalendarId = calendarId,
            Title = TrimTitle(title),
            Description = description,
            Location = location,
            Url = url,
            StartsAt = startsAt,
            EndsAt = endsAt,
            IsAllDay = isAllDay,
            TimeZone = zone,
            Rrule = storedRrule,
            RecurrenceEndsAt = recurrenceEndsAt,
            Status = status,
            CreatedAt = timeProvider.GetUtcNow()
        };
    }

    /// <summary>Edits the whole event (the <c>all</c> scope). Keeps the recurrence rule intact.</summary>
    public void Update(
        string title, string? description, string? location, string? url,
        DateTimeOffset startsAt, DateTimeOffset endsAt, bool isAllDay, Guid calendarId)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("An event needs a title.", nameof(title));

        startsAt = startsAt.ToUniversalTime();
        endsAt = endsAt.ToUniversalTime();
        if (endsAt < startsAt)
            throw new ArgumentException("An event cannot end before it starts.", nameof(endsAt));

        Title = TrimTitle(title);
        Description = description;
        Location = location;
        Url = url;
        StartsAt = startsAt;
        EndsAt = endsAt;
        IsAllDay = isAllDay;
        CalendarId = calendarId;

        if (IsRecurring)
            RecurrenceEndsAt = RecurrenceRule.Parse(Rrule!).ComputeEndsAt(StartsAt, ResolveZone());
    }

    /// <summary>
    /// Ends the series just before <paramref name="cutInstant"/> by rewriting the rule with an
    /// <c>UNTIL</c>. The "this and future" split calls this on the original event; the caller then
    /// creates a fresh event to carry the tail.
    /// </summary>
    public void EndSeriesBefore(DateTimeOffset cutInstant)
    {
        if (!IsRecurring)
            throw new InvalidOperationException("Only a recurring event can be split.");

        var rule = RecurrenceRule.Parse(Rrule!);
        // UNTIL is inclusive, so end one second before the cut occurrence.
        var until = cutInstant.ToUniversalTime().AddSeconds(-1);
        Rrule = rule.WithUntil(until);
        RecurrenceEndsAt = RecurrenceRule.Parse(Rrule).ComputeEndsAt(StartsAt, ResolveZone());
    }

    /// <summary>Soft-deletes the event (the <c>all</c> delete scope).</summary>
    public void Delete(TimeProvider timeProvider) => DeletedAt ??= timeProvider.GetUtcNow();

    /// <summary>The event's zone as a <see cref="TimeZoneInfo"/>, throwing on an unknown IANA id.</summary>
    public TimeZoneInfo ResolveZone() => ResolveZone(TimeZone);

    private static TimeZoneInfo ResolveZone(string timeZone)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZone);
        }
        catch (TimeZoneNotFoundException)
        {
            throw new ArgumentException($"Unknown IANA time zone '{timeZone}'.", nameof(timeZone));
        }
    }

    private static string TrimTitle(string title) =>
        title.Length <= MaxTitleLength ? title.Trim() : title[..MaxTitleLength].Trim();
}
