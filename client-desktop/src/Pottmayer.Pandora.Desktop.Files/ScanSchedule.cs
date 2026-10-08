namespace Pottmayer.Pandora.Desktop.Files;

/// <summary>When a root's daily scan is due, in the device's local time.</summary>
internal static class ScanSchedule
{
    /// <summary>After an attempt that failed (server or network down), the next try waits this long.</summary>
    public static readonly TimeSpan RetryAfter = TimeSpan.FromHours(1);

    /// <summary>
    /// Due when the latest scheduled moment has passed with no scan completed since — so a PC that was off
    /// at 03:00 scans when it comes back. One attempt per scheduled moment: a scan the backend held or
    /// aborted waits for the next day (or "Scan now"); only a failed attempt retries, after an hour.
    /// A root that never completed a scan is not scanned by schedule: its first scan is the user's
    /// "Scan now", after the selection is set, so a whole disk is never cataloged by accident.
    /// </summary>
    public static bool IsDue(
        TimeOnly? scanTime, DateTimeOffset? lastCompleted, DateTimeOffset? lastAttempt, bool lastAttemptFailed, DateTimeOffset now)
    {
        if (scanTime is not { } at || lastCompleted is not { } completed) return false;

        var occurrence = LatestOccurrence(at, now);
        if (completed >= occurrence) return false;

        return lastAttempt switch
        {
            null => true,
            { } attempt when lastAttemptFailed => now - attempt >= RetryAfter,
            { } attempt => attempt < occurrence,
        };
    }

    /// <summary>The latest moment at <paramref name="at"/> that is not after <paramref name="now"/>.</summary>
    public static DateTimeOffset LatestOccurrence(TimeOnly at, DateTimeOffset now)
    {
        var today = new DateTimeOffset(now.Date + at.ToTimeSpan(), now.Offset);
        return today <= now ? today : today.AddDays(-1);
    }
}
