using Pottmayer.Pandora.Modules.Finances.Domain.ValueObjects;
using Xunit;

namespace Pottmayer.Pandora.Modules.Finances.Tests;

public sealed class RecurrenceRuleTests
{
    private static RecurrenceRule Daily(short interval = 1) =>
        RecurrenceRule.Create(RecurrenceFrequency.Daily, interval, null, null, new DateOnly(2026, 1, 1), null, null);

    private static RecurrenceRule Weekly(short interval = 1) =>
        RecurrenceRule.Create(RecurrenceFrequency.Weekly, interval, null, null, new DateOnly(2026, 1, 1), null, null);

    private static RecurrenceRule Monthly(short interval = 1, short? dayOfMonth = null) =>
        RecurrenceRule.Create(RecurrenceFrequency.Monthly, interval, dayOfMonth, null, new DateOnly(2026, 1, 1), null, null);

    private static RecurrenceRule Yearly(short interval = 1, short? dayOfMonth = null) =>
        RecurrenceRule.Create(RecurrenceFrequency.Yearly, interval, dayOfMonth, null, new DateOnly(2026, 1, 1), null, null);

    [Theory]
    [InlineData("daily", true)]
    [InlineData("weekly", true)]
    [InlineData("monthly", true)]
    [InlineData("yearly", true)]
    [InlineData("hourly", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsSupported_returns_expected(string? value, bool expected) =>
        Assert.Equal(expected, RecurrenceFrequency.IsSupported(value));

    [Fact]
    public void Daily_advances_by_interval_days()
    {
        var rule = Daily(3);
        var next = rule.NextOccurrenceAfter(new DateOnly(2026, 6, 1));
        Assert.Equal(new DateOnly(2026, 6, 4), next);
    }

    [Fact]
    public void Weekly_advances_by_interval_weeks()
    {
        var rule = Weekly(2);
        var next = rule.NextOccurrenceAfter(new DateOnly(2026, 6, 1));
        Assert.Equal(new DateOnly(2026, 6, 15), next);
    }

    [Fact]
    public void Monthly_advances_preserving_day()
    {
        var rule = Monthly(1);
        var next = rule.NextOccurrenceAfter(new DateOnly(2026, 1, 15));
        Assert.Equal(new DateOnly(2026, 2, 15), next);
    }

    [Fact]
    public void Monthly_uses_explicit_DayOfMonth()
    {
        var rule = Monthly(1, dayOfMonth: 10);
        var next = rule.NextOccurrenceAfter(new DateOnly(2026, 1, 5));
        Assert.Equal(new DateOnly(2026, 2, 10), next);
    }

    [Fact]
    public void Monthly_clamps_day31_to_end_of_February()
    {
        var rule = Monthly(1, dayOfMonth: 31);
        // Jan 31 → Feb: clamp 31 to 28 (2026 is not a leap year)
        var next = rule.NextOccurrenceAfter(new DateOnly(2026, 1, 31));
        Assert.Equal(new DateOnly(2026, 2, 28), next);
    }

    [Fact]
    public void Monthly_clamps_day31_to_leap_February()
    {
        var rule = Monthly(1, dayOfMonth: 31);
        // Jan 31 → Feb: clamp 31 to 29 (2028 is a leap year)
        var next = rule.NextOccurrenceAfter(new DateOnly(2028, 1, 31));
        Assert.Equal(new DateOnly(2028, 2, 29), next);
    }

    [Fact]
    public void Monthly_clamps_day30_in_February()
    {
        var rule = Monthly(1, dayOfMonth: 30);
        var next = rule.NextOccurrenceAfter(new DateOnly(2026, 1, 30));
        Assert.Equal(new DateOnly(2026, 2, 28), next);
    }

    [Fact]
    public void Monthly_does_not_clamp_day_in_31_day_month()
    {
        var rule = Monthly(1, dayOfMonth: 31);
        var next = rule.NextOccurrenceAfter(new DateOnly(2026, 2, 28));
        Assert.Equal(new DateOnly(2026, 3, 31), next);
    }

    [Fact]
    public void Yearly_advances_by_interval_years()
    {
        var rule = Yearly(1);
        var next = rule.NextOccurrenceAfter(new DateOnly(2026, 6, 15));
        Assert.Equal(new DateOnly(2027, 6, 15), next);
    }

    [Fact]
    public void Yearly_clamps_Feb29_on_non_leap_year()
    {
        var rule = Yearly(1, dayOfMonth: 29);
        // March 2024 → March 2025: day 29 in February — but yearly advances month = same month
        // Let's test: Feb 29, 2024 → Feb 28, 2025 (2025 is not leap)
        var rule2 = RecurrenceRule.Create(RecurrenceFrequency.Yearly, 1, 29, null, new DateOnly(2024, 2, 29), null, null);
        var next = rule2.NextOccurrenceAfter(new DateOnly(2024, 2, 29));
        Assert.Equal(new DateOnly(2025, 2, 28), next);
    }

    [Fact]
    public void IsTerminated_false_when_no_limits()
    {
        var rule = Monthly();
        Assert.False(rule.IsTerminated(new DateOnly(2099, 1, 1), 999));
    }

    [Fact]
    public void IsTerminated_true_when_count_reaches_maxOccurrences()
    {
        var rule = RecurrenceRule.Create(RecurrenceFrequency.Monthly, 1, null, null, new DateOnly(2026, 1, 1), null, 3);
        Assert.False(rule.IsTerminated(new DateOnly(2026, 4, 1), 2));
        Assert.True(rule.IsTerminated(new DateOnly(2026, 4, 1), 3));
    }

    [Fact]
    public void IsTerminated_true_when_nextDate_exceeds_endDate()
    {
        var endDate = new DateOnly(2026, 6, 30);
        var rule = RecurrenceRule.Create(RecurrenceFrequency.Monthly, 1, null, null, new DateOnly(2026, 1, 1), endDate, null);
        Assert.False(rule.IsTerminated(new DateOnly(2026, 6, 30), 5));
        Assert.True(rule.IsTerminated(new DateOnly(2026, 7, 1), 5));
    }

    [Fact]
    public void IsTerminated_true_when_both_limits_reached_via_maxOccurrences()
    {
        var rule = RecurrenceRule.Create(RecurrenceFrequency.Monthly, 1, null, null, new DateOnly(2026, 1, 1),
            new DateOnly(2027, 1, 1), 3);
        // Count limit reached first
        Assert.True(rule.IsTerminated(new DateOnly(2026, 4, 1), 3));
    }
}
