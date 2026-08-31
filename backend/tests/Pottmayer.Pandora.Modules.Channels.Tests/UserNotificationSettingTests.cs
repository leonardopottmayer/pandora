using Pottmayer.Pandora.Modules.Channels.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Channels.Domain.ValueObjects;
using Pottmayer.Pandora.Modules.Channels.Tests.Fakes;
using Xunit;

namespace Pottmayer.Pandora.Modules.Channels.Tests;

public sealed class UserNotificationSettingTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static UserNotificationSetting With(TimeOnly start, TimeOnly end, QuietHoursBehaviour behaviour)
    {
        var setting = UserNotificationSetting.Create(Guid.NewGuid(), new FixedTimeProvider(Now));
        setting.SetQuietHours(start, end, behaviour);
        return setting;
    }

    [Fact]
    public void A_fresh_setting_has_quiet_hours_off()
    {
        var setting = UserNotificationSetting.Create(Guid.NewGuid(), new FixedTimeProvider(Now));

        Assert.False(setting.QuietHoursEnabled);
        Assert.False(setting.IsWithinQuietHours(new TimeOnly(3, 0)));
        Assert.False(setting.ShouldSuppress(new TimeOnly(3, 0)));
    }

    [Theory]
    [InlineData(8, 0, true)]    // start is inclusive
    [InlineData(12, 0, true)]
    [InlineData(17, 59, true)]
    [InlineData(18, 0, false)]  // end is exclusive
    [InlineData(7, 59, false)]
    public void Same_day_window_includes_start_excludes_end(int hour, int minute, bool inside)
    {
        var setting = With(new TimeOnly(8, 0), new TimeOnly(18, 0), QuietHoursBehaviour.Suppress);

        Assert.Equal(inside, setting.IsWithinQuietHours(new TimeOnly(hour, minute)));
    }

    [Theory]
    [InlineData(22, 0, true)]   // start is inclusive
    [InlineData(23, 30, true)]
    [InlineData(0, 0, true)]    // just past midnight
    [InlineData(6, 59, true)]
    [InlineData(7, 0, false)]   // end is exclusive
    [InlineData(12, 0, false)]  // middle of the day
    public void Overnight_window_wraps_past_midnight(int hour, int minute, bool inside)
    {
        var setting = With(new TimeOnly(22, 0), new TimeOnly(7, 0), QuietHoursBehaviour.Suppress);

        Assert.Equal(inside, setting.IsWithinQuietHours(new TimeOnly(hour, minute)));
    }

    [Fact]
    public void Deliver_anyway_never_suppresses_even_inside_the_window()
    {
        var setting = With(new TimeOnly(8, 0), new TimeOnly(18, 0), QuietHoursBehaviour.DeliverAnyway);

        Assert.True(setting.IsWithinQuietHours(new TimeOnly(12, 0)));
        Assert.False(setting.ShouldSuppress(new TimeOnly(12, 0)));
    }

    [Fact]
    public void Clearing_turns_quiet_hours_off()
    {
        var setting = With(new TimeOnly(8, 0), new TimeOnly(18, 0), QuietHoursBehaviour.Suppress);
        setting.ClearQuietHours();

        Assert.False(setting.QuietHoursEnabled);
        Assert.Null(setting.QuietHoursStart);
        Assert.Null(setting.QuietHoursEnd);
        Assert.Null(setting.QuietHoursBehaviour);
        Assert.False(setting.ShouldSuppress(new TimeOnly(12, 0)));
    }
}
