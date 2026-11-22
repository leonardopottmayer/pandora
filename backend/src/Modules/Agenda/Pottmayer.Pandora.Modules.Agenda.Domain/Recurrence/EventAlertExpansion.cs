using Pottmayer.Pandora.Modules.Agenda.Domain.Aggregates;

namespace Pottmayer.Pandora.Modules.Agenda.Domain.Recurrence;

/// <summary>
/// The pure core of the event-alert sweep: which occurrence anchors of one event are due to fire for an
/// alert of a given offset, within <c>[windowStart, now]</c>. Unlike a task (a single materialized row),
/// an event is expanded here, exactly as a recurring reminder is.
///
/// <para>An alert fires relative to the occurrence's <b>on-grid</b> start (the dispatch key), so the
/// window pre-filter stays valid; a cancelled occurrence (an override with <c>IsCancelled</c>) is
/// suppressed. A per-occurrence reschedule moves the event on the calendar but not the alert — a
/// documented Phase-4 limitation. Idempotency (one fire per anchor) is the agd008 ledger, applied by
/// the handler; this method is oblivious to it.</para>
/// </summary>
public static class EventAlertExpansion
{
    /// <summary>
    /// The occurrence anchors (each an <c>occurrence_starts_at</c>) whose firing instant
    /// (<c>anchor + offset</c>) lands in <c>[windowStart, now]</c>, minus cancelled occurrences.
    /// </summary>
    public static IReadOnlyList<DateTimeOffset> DueOccurrences(
        Event ev,
        int offsetMinutes,
        IReadOnlyList<EventOccurrenceOverride> overrides,
        DateTimeOffset windowStart,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(ev);

        // firing = occ + offset ∈ [windowStart, now]  ⟺  occ ∈ [windowStart - offset, now - offset]
        var offset = TimeSpan.FromMinutes(offsetMinutes);
        var occFrom = windowStart - offset;
        var occTo = now - offset;
        if (occTo < occFrom)
            return [];

        IEnumerable<DateTimeOffset> grid;
        if (ev.IsRecurring)
        {
            var rule = RecurrenceRule.Parse(ev.Rrule!);
            grid = rule.Expand(ev.StartsAt, occFrom, occTo, ev.ResolveZone());
        }
        else
        {
            grid = ev.StartsAt >= occFrom && ev.StartsAt <= occTo ? [ev.StartsAt] : [];
        }

        var cancelled = overrides
            .Where(o => o.IsCancelled)
            .Select(o => o.OriginalStartsAt)
            .ToHashSet();

        return [.. grid.Where(occ => !cancelled.Contains(occ))];
    }
}
