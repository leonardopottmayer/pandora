using Xunit;

namespace Pottmayer.Pandora.Desktop.Files.Tests;

public sealed class ScanScheduleTests
{
    private static readonly TimeOnly ThreeAm = new(3, 0);
    private static readonly TimeSpan Offset = TimeSpan.FromHours(-3);

    private static DateTimeOffset At(int day, int hour, int minute = 0) => new(2026, 10, day, hour, minute, 0, Offset);

    [Fact]
    public void A_scan_completed_since_the_last_scheduled_time_is_enough() =>
        Assert.False(ScanSchedule.IsDue(ThreeAm, At(8, 3, 20), null, false, At(8, 10)));

    [Fact]
    public void Due_once_the_time_passes_without_a_completed_scan() =>
        Assert.True(ScanSchedule.IsDue(ThreeAm, At(7, 3, 20), null, false, At(8, 3, 1)));

    [Fact]
    public void Not_due_before_the_time_when_last_night_was_covered() =>
        Assert.False(ScanSchedule.IsDue(ThreeAm, At(7, 3, 20), null, false, At(8, 2, 59)));

    [Fact]
    public void A_pc_that_was_off_catches_up_when_it_comes_back() =>
        Assert.True(ScanSchedule.IsDue(ThreeAm, At(5, 3, 20), null, false, At(8, 1)));

    [Fact]
    public void A_root_never_scanned_waits_for_scan_now() =>
        Assert.False(ScanSchedule.IsDue(ThreeAm, null, null, false, At(8, 10)));

    [Fact]
    public void Manual_only_roots_are_never_due() =>
        Assert.False(ScanSchedule.IsDue(null, At(1, 3), null, false, At(8, 10)));

    [Fact]
    public void A_held_or_aborted_scan_waits_for_the_next_day() =>
        Assert.False(ScanSchedule.IsDue(ThreeAm, At(7, 3, 20), At(8, 3, 5), false, At(8, 15)));

    [Fact]
    public void A_failed_attempt_retries_after_an_hour()
    {
        Assert.False(ScanSchedule.IsDue(ThreeAm, At(7, 3, 20), At(8, 3, 5), true, At(8, 3, 30)));
        Assert.True(ScanSchedule.IsDue(ThreeAm, At(7, 3, 20), At(8, 3, 5), true, At(8, 4, 5)));
    }
}
