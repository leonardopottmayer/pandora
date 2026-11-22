using Pottmayer.Pandora.Modules.Identity.Abstractions.Ports;

namespace Pottmayer.Pandora.Modules.Finances.Application.Services;

/// <summary>
/// "Today" for each user in a pass that spans every user (the daily jobs): each user's own calendar
/// day in their zone, looked up once per user. A <c>fixedToday</c> (tests, a manual run) applies to
/// everyone instead.
/// </summary>
internal sealed class UserDays(IEffectiveTimeZoneResolver timeZones, DateTimeOffset now, DateOnly? fixedToday)
{
    private readonly Dictionary<Guid, DateOnly> _byUser = [];

    /// <summary>
    /// No user is past this date: the zones furthest ahead run 14 hours before UTC, never a full day.
    /// Queries fetch up to it, then each row is checked against its own user's day.
    /// </summary>
    public DateOnly Latest => fixedToday ?? DateOnly.FromDateTime(now.UtcDateTime).AddDays(1);

    public async Task<DateOnly> TodayAsync(Guid userId, CancellationToken ct)
    {
        if (fixedToday is { } fixedDay)
            return fixedDay;
        if (!_byUser.TryGetValue(userId, out var today))
            _byUser[userId] = today = await timeZones.ResolveTodayAsync(userId, now, ct);
        return today;
    }
}
