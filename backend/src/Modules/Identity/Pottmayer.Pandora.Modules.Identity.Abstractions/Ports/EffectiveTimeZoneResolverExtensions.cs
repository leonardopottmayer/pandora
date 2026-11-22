namespace Pottmayer.Pandora.Modules.Identity.Abstractions.Ports;

/// <summary>Convenience over <see cref="IEffectiveTimeZoneResolver"/> for the common "today" question.</summary>
public static class EffectiveTimeZoneResolverExtensions
{
    /// <summary>
    /// The user's current calendar date: <paramref name="now"/> converted to the user's effective zone,
    /// then reduced to a wall date. Use it wherever "today" must be the user's day, not the UTC day — a
    /// UTC−3 user at 22:00 local is already on the next UTC date, so <c>now.UtcDateTime.Date</c> misfiles
    /// late-evening items by a day.
    /// </summary>
    public static async Task<DateOnly> ResolveTodayAsync(
        this IEffectiveTimeZoneResolver resolver, Guid userId, DateTimeOffset now, CancellationToken ct = default)
    {
        var zone = await resolver.ResolveAsync(userId, ct: ct);
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, zone).DateTime);
    }
}
