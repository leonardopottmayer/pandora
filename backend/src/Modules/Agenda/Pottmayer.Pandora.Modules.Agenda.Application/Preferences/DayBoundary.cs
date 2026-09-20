namespace Pottmayer.Pandora.Modules.Agenda.Application.Preferences;

/// <summary>
/// Turns "the user's calendar day" into UTC instants. "Today" for a UTC−3 user is not the UTC day:
/// 22:00 local is already tomorrow in UTC, so a day window taken in UTC drops or misfiles late-evening
/// items. These helpers anchor the window in the user's zone (DST-correct at each edge, since the
/// offset is taken at that specific local midnight) so day-scoped reads bucket by the user's day.
/// </summary>
internal static class DayBoundary
{
    /// <summary>The user's current calendar date, as a wall date in their zone.</summary>
    public static DateOnly LocalToday(TimeZoneInfo zone, DateTimeOffset now)
        => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, zone).DateTime);

    /// <summary>The instant at which <paramref name="localDate"/> starts in the user's zone.</summary>
    public static DateTimeOffset StartOfDay(TimeZoneInfo zone, DateOnly localDate)
    {
        var midnight = localDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        return new DateTimeOffset(midnight, zone.GetUtcOffset(midnight));
    }
}
