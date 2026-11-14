namespace Pottmayer.Pandora.Modules.Agenda.Domain.Recurrence;

/// <summary>
/// An all-day value (a task due on a day, an all-day event) is a date, not an instant: "Nov 14" is
/// Nov 14 wherever the user is. It is stored as the midnight that starts that date in the item's own
/// zone, and read back as the midnight that starts the same date in the viewer's zone, so a viewer in
/// another zone still sees it on Nov 14 instead of on the day the stored instant falls on there.
/// </summary>
public static class AllDayDates
{
    /// <summary>
    /// The date the caller meant. An instant that carries an offset (<c>2026-11-14T00:00:00-03:00</c>)
    /// is read on its own wall clock, so the date survives whatever zone the item lives in. A UTC
    /// instant (offset zero, which is also how stored values come back from the database) is read in
    /// <paramref name="zone"/>, the item's zone.
    /// </summary>
    /// <remarks>
    /// ponytail: a caller whose own zone is UTC and whose item lives in another zone reads in the item's
    /// zone; callers we control send a wall time other than midnight (or the item's own zone), so it holds.
    /// </remarks>
    public static DateOnly DateOf(DateTimeOffset value, TimeZoneInfo zone) =>
        value.Offset != TimeSpan.Zero
            ? DateOnly.FromDateTime(value.DateTime)
            : DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(value, zone).DateTime);

    /// <summary>The instant <paramref name="date"/> starts in <paramref name="zone"/>, with that zone's offset.</summary>
    public static DateTimeOffset StartOf(DateOnly date, TimeZoneInfo zone)
    {
        var midnight = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        // A zone whose DST jump skips midnight starts that day at the first wall time that exists.
        while (zone.IsInvalidTime(midnight))
            midnight = midnight.AddMinutes(30);
        return new DateTimeOffset(midnight, zone.GetUtcOffset(midnight));
    }

    /// <summary>A date-only due value snapped to the start of its date in the item's zone, in UTC.</summary>
    public static DateTimeOffset Snap(DateTimeOffset value, TimeZoneInfo zone) =>
        StartOf(DateOf(value, zone), zone).ToUniversalTime();

    /// <summary>
    /// An all-day span snapped to whole days in the item's zone, end exclusive: from the start of the
    /// day <paramref name="start"/> falls on to the start of the day after the last day. An end that is
    /// exactly a day's start is already that exclusive bound (Google and stored rows send it so); any
    /// other end names the last day itself. Always at least one day.
    /// </summary>
    public static (DateTimeOffset Start, DateTimeOffset End) SnapSpan(DateTimeOffset start, DateTimeOffset end, TimeZoneInfo zone)
    {
        var firstDay = DateOf(start, zone);
        var endDay = DateOf(end, zone);
        var endIsDayStart = end.Offset != TimeSpan.Zero
            ? end.TimeOfDay == TimeSpan.Zero
            : end == StartOf(endDay, zone);
        var afterLast = endIsDayStart ? endDay : endDay.AddDays(1);
        if (afterLast <= firstDay)
            afterLast = firstDay.AddDays(1);
        return (StartOf(firstDay, zone).ToUniversalTime(), StartOf(afterLast, zone).ToUniversalTime());
    }

    /// <summary>
    /// A stored all-day span (whole days in <paramref name="itemZone"/>) moved onto the same dates in
    /// <paramref name="viewerZone"/>. The day count is rounded, so a span that crossed a DST change
    /// (23 or 25 hours a day) still counts whole days.
    /// </summary>
    public static (DateTimeOffset Start, DateTimeOffset End) Reanchor(
        DateTimeOffset start, DateTimeOffset end, TimeZoneInfo itemZone, TimeZoneInfo viewerZone)
    {
        var firstDay = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(start, itemZone).DateTime);
        var days = Math.Max(1, (int)Math.Round((end - start).TotalDays));
        return (StartOf(firstDay, viewerZone), StartOf(firstDay.AddDays(days), viewerZone));
    }
}
