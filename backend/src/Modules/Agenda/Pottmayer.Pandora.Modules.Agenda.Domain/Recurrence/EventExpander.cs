using Pottmayer.Pandora.Modules.Agenda.Domain.Aggregates;

namespace Pottmayer.Pandora.Modules.Agenda.Domain.Recurrence;

/// <summary>
/// Expands an <see cref="Event"/> into the concrete occurrences overlapping a window, applying its
/// overrides. Pure and DST-aware (it defers wall-clock handling to <see cref="RecurrenceRule"/>): a
/// cancelled occurrence disappears, an edited one has its fields overridden, and every occurrence keeps
/// the series duration unless the override changes the end. This is the read model behind
/// <c>GET /agenda/events</c> and <c>GET /agenda/today</c>.
/// </summary>
public static class EventExpander
{
    /// <summary>
    /// Every occurrence whose <c>[start, end)</c> overlaps <c>[from, to]</c>, ascending by start.
    /// <paramref name="overrides"/> are this event's rows (cancellations and per-occurrence edits).
    /// With a <paramref name="viewerZone"/>, an all-day occurrence comes back on the same dates in that
    /// zone (<see cref="AllDayDates.Reanchor"/>) and is matched against the window there, so it stays on
    /// its day for a viewer in another zone. <see cref="EventOccurrence.OriginalStartsAt"/> is never
    /// moved: it is the key edits use.
    /// </summary>
    public static IReadOnlyList<EventOccurrence> Expand(
        Event ev,
        IReadOnlyList<EventOccurrenceOverride> overrides,
        DateTimeOffset from,
        DateTimeOffset to,
        TimeZoneInfo? viewerZone = null)
    {
        ArgumentNullException.ThrowIfNull(ev);
        if (to < from)
            return [];

        var duration = ev.Duration;
        var byOriginal = new Dictionary<DateTimeOffset, EventOccurrenceOverride>();
        foreach (var ov in overrides)
            byOriginal[ov.OriginalStartsAt] = ov;

        // Widen the start window by the duration so an occurrence that began before `from` but is still
        // running is included.
        var gridFrom = from - duration;
        var reanchor = ev.IsAllDay && viewerZone is not null;
        // The same date sits up to a day apart across zones, so an all-day grid looks a day further each way.
        var gridTo = reanchor ? to.AddDays(1) : to;
        if (reanchor)
            gridFrom = gridFrom.AddDays(-1);

        IEnumerable<DateTimeOffset> grid;
        if (ev.IsRecurring)
        {
            var rule = RecurrenceRule.Parse(ev.Rrule!);
            grid = rule.Expand(ev.StartsAt, gridFrom, gridTo, ev.ResolveZone());
        }
        else
        {
            grid = ev.StartsAt >= gridFrom && ev.StartsAt <= gridTo ? [ev.StartsAt] : [];
        }

        var result = new List<EventOccurrence>();
        foreach (var occ in grid)
        {
            byOriginal.TryGetValue(occ, out var ov);
            if (ov is { IsCancelled: true })
                continue;

            var startsAt = ov?.StartsAt ?? occ;
            var endsAt = ov?.EndsAt ?? startsAt + duration;

            if (reanchor)
            {
                (startsAt, endsAt) = AllDayDates.Reanchor(startsAt, endsAt, ev.ResolveZone(), viewerZone!);
                // Whole days end exclusive: one that ends where the window starts is the day before.
                if (startsAt > to || endsAt <= from)
                    continue;
            }
            // Final overlap check against the requested window (a reschedule may move the occurrence).
            else if (startsAt > to || endsAt < from)
                continue;

            result.Add(new EventOccurrence(
                ev.Id,
                ev.CalendarId,
                occ,
                startsAt,
                endsAt,
                ev.IsAllDay,
                ov?.Title ?? ev.Title,
                ov?.Description ?? ev.Description,
                ov?.Location ?? ev.Location,
                ev.Url,
                ev.Status));
        }

        result.Sort((a, b) => a.StartsAt.CompareTo(b.StartsAt));
        return result;
    }
}
