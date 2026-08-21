using Pottmayer.Pandora.Modules.Agenda.Domain.Recurrence;
using Xunit;

namespace Pottmayer.Pandora.Modules.Agenda.Tests;

/// <summary>
/// The recurrence engine's contract, table-driven. Written before anything else in the module leans on
/// it: parsing (what the subset accepts and rejects), expansion per frequency, WKST, ordinals like
/// <c>-1FR</c>, and — the reason the engine exists — that a wall-clock reminder fires exactly once per
/// day across a daylight-saving change.
/// </summary>
public sealed class RecurrenceRuleTests
{
    private static readonly TimeZoneInfo Utc = TimeZoneInfo.Utc;
    private static readonly TimeZoneInfo NewYork = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");

    // A local wall-clock instant in a zone, so tests read in the reminder's own time.
    private static DateTimeOffset Local(TimeZoneInfo zone, int year, int month, int day, int hour = 9, int minute = 0)
    {
        var wall = new DateTime(year, month, day, hour, minute, 0, DateTimeKind.Unspecified);
        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(wall, zone), TimeSpan.Zero);
    }

    private static List<DateTimeOffset> Expand(
        string rrule, DateTimeOffset start, DateTimeOffset from, DateTimeOffset to, TimeZoneInfo zone) =>
        RecurrenceRule.Parse(rrule).Expand(start, from, to, zone).ToList();

    // ── Parsing: the write-time guard ──

    [Theory]
    [InlineData("FREQ=DAILY")]
    [InlineData("FREQ=WEEKLY;BYDAY=MO,TU,WE,TH,FR")]
    [InlineData("FREQ=MONTHLY;BYDAY=-1FR")]
    [InlineData("FREQ=YEARLY;BYMONTH=3;BYMONTHDAY=15")]
    [InlineData("RRULE:FREQ=DAILY;INTERVAL=2;COUNT=5")]
    [InlineData("FREQ=WEEKLY;INTERVAL=2;BYDAY=TU,SU;WKST=SU")]
    [InlineData("FREQ=DAILY;UNTIL=20260401T000000Z")]
    public void Accepts_supported_rules(string rrule) => Assert.True(RecurrenceRule.IsValid(rrule));

    [Theory]
    [InlineData("")]
    [InlineData("INTERVAL=2")]                         // no FREQ
    [InlineData("FREQ=HOURLY")]                        // unsupported frequency
    [InlineData("FREQ=DAILY;BYSETPOS=1")]
    [InlineData("FREQ=WEEKLY;BYWEEKNO=3")]
    [InlineData("FREQ=YEARLY;BYYEARDAY=100")]
    [InlineData("FREQ=DAILY;BYHOUR=9")]
    [InlineData("FREQ=DAILY;BYMINUTE=30")]
    [InlineData("FREQ=DAILY;BYSECOND=0")]
    [InlineData("FREQ=DAILY;COUNT=3;UNTIL=20260401T000000Z")] // mutually exclusive
    [InlineData("FREQ=DAILY;BYDAY=2TU")]               // ordinal BYDAY needs MONTHLY/YEARLY
    [InlineData("FREQ=WEEKLY;BYDAY=-1FR")]
    [InlineData("FREQ=DAILY;INTERVAL=0")]
    [InlineData("FREQ=MONTHLY;BYMONTHDAY=0")]
    public void Rejects_unsupported_or_malformed_rules(string rrule) => Assert.False(RecurrenceRule.IsValid(rrule));

    [Fact]
    public void Parse_keeps_the_raw_string_verbatim()
    {
        const string raw = "FREQ=WEEKLY;BYDAY=MO,WE;WKST=SU";
        Assert.Equal(raw, RecurrenceRule.Parse(raw).Raw);
    }

    // ── DAILY ──

    [Fact]
    public void Daily_with_interval_fires_every_nth_day()
    {
        var start = Local(Utc, 2026, 1, 1);
        var occ = Expand("FREQ=DAILY;INTERVAL=3", start, Local(Utc, 2026, 1, 1), Local(Utc, 2026, 1, 10), Utc);

        Assert.Equal(
            [Local(Utc, 2026, 1, 1), Local(Utc, 2026, 1, 4), Local(Utc, 2026, 1, 7), Local(Utc, 2026, 1, 10)],
            occ);
    }

    [Fact]
    public void Count_is_measured_from_the_series_start_not_the_window()
    {
        var start = Local(Utc, 2026, 1, 1);
        // Window opens after the series began; COUNT=3 still means occurrences #1..#3 (Jan 1,2,3), so a
        // window over Jan 2..Jan 10 yields only #2 and #3.
        var occ = Expand("FREQ=DAILY;COUNT=3", start, Local(Utc, 2026, 1, 2), Local(Utc, 2026, 1, 10), Utc);

        Assert.Equal([Local(Utc, 2026, 1, 2), Local(Utc, 2026, 1, 3)], occ);
    }

    [Fact]
    public void Until_bounds_the_series_inclusive()
    {
        var start = Local(Utc, 2026, 1, 1);
        var occ = Expand("FREQ=DAILY;UNTIL=20260103T090000Z", start, start, Local(Utc, 2026, 1, 10), Utc);

        Assert.Equal([Local(Utc, 2026, 1, 1), Local(Utc, 2026, 1, 2), Local(Utc, 2026, 1, 3)], occ);
    }

    // ── WEEKLY ──

    [Fact]
    public void Weekly_byday_fires_each_named_weekday()
    {
        var start = Local(Utc, 2026, 1, 5); // Monday
        var occ = Expand("FREQ=WEEKLY;BYDAY=MO,WE,FR", start, start, Local(Utc, 2026, 1, 11), Utc);

        Assert.Equal(
            [Local(Utc, 2026, 1, 5), Local(Utc, 2026, 1, 7), Local(Utc, 2026, 1, 9)],
            occ);
    }

    // The canonical RFC 5545 example: only WKST changes, and the result changes with it.
    [Fact]
    public void Wkst_monday_groups_the_biweekly_span_one_way()
    {
        var start = Local(Utc, 1997, 8, 5); // Tuesday
        var occ = Expand("FREQ=WEEKLY;INTERVAL=2;COUNT=4;BYDAY=TU,SU;WKST=MO",
            start, start, Local(Utc, 1997, 9, 30), Utc);

        Assert.Equal(
            [Local(Utc, 1997, 8, 5), Local(Utc, 1997, 8, 10), Local(Utc, 1997, 8, 19), Local(Utc, 1997, 8, 24)],
            occ);
    }

    [Fact]
    public void Wkst_sunday_groups_the_biweekly_span_the_other_way()
    {
        var start = Local(Utc, 1997, 8, 5); // Tuesday
        var occ = Expand("FREQ=WEEKLY;INTERVAL=2;COUNT=4;BYDAY=TU,SU;WKST=SU",
            start, start, Local(Utc, 1997, 9, 30), Utc);

        Assert.Equal(
            [Local(Utc, 1997, 8, 5), Local(Utc, 1997, 8, 17), Local(Utc, 1997, 8, 19), Local(Utc, 1997, 8, 31)],
            occ);
    }

    // ── MONTHLY ──

    [Fact]
    public void Monthly_bymonthday_supports_negative_last_day()
    {
        var start = Local(Utc, 2026, 1, 31);
        var occ = Expand("FREQ=MONTHLY;BYMONTHDAY=-1", start, start, Local(Utc, 2026, 3, 31), Utc);

        // Last day of Jan, Feb, Mar — Feb correctly lands on the 28th, not a skipped 31st.
        Assert.Equal(
            [Local(Utc, 2026, 1, 31), Local(Utc, 2026, 2, 28), Local(Utc, 2026, 3, 31)],
            occ);
    }

    [Fact]
    public void Monthly_byday_second_tuesday()
    {
        var start = Local(Utc, 2026, 1, 1);
        var occ = Expand("FREQ=MONTHLY;BYDAY=2TU", start, start, Local(Utc, 2026, 3, 31), Utc);

        Assert.Equal(
            [Local(Utc, 2026, 1, 13), Local(Utc, 2026, 2, 10), Local(Utc, 2026, 3, 10)],
            occ);
    }

    [Fact]
    public void Monthly_byday_last_friday()
    {
        var start = Local(Utc, 2026, 1, 1);
        var occ = Expand("FREQ=MONTHLY;BYDAY=-1FR", start, start, Local(Utc, 2026, 3, 31), Utc);

        Assert.Equal(
            [Local(Utc, 2026, 1, 30), Local(Utc, 2026, 2, 27), Local(Utc, 2026, 3, 27)],
            occ);
    }

    [Fact]
    public void Monthly_anchor_day_skips_months_that_are_too_short()
    {
        var start = Local(Utc, 2026, 1, 31);
        var occ = Expand("FREQ=MONTHLY", start, start, Local(Utc, 2026, 4, 30), Utc);

        // Jan 31 and Mar 31 fire; Feb and Apr have no 31st, so they are skipped, not clamped.
        Assert.Equal([Local(Utc, 2026, 1, 31), Local(Utc, 2026, 3, 31)], occ);
    }

    // ── YEARLY ──

    [Fact]
    public void Yearly_on_a_fixed_month_and_day()
    {
        var start = Local(Utc, 2026, 3, 15);
        var occ = Expand("FREQ=YEARLY", start, start, Local(Utc, 2029, 1, 1), Utc);

        Assert.Equal(
            [Local(Utc, 2026, 3, 15), Local(Utc, 2027, 3, 15), Local(Utc, 2028, 3, 15)],
            occ);
    }

    // ── ComputeEndsAt (the denormalized recurrence bound) ──

    [Fact]
    public void Ends_at_is_null_for_an_unbounded_rule()
    {
        var start = Local(Utc, 2026, 1, 1);
        Assert.Null(RecurrenceRule.Parse("FREQ=DAILY").ComputeEndsAt(start, Utc));
    }

    [Fact]
    public void Ends_at_is_the_count_th_occurrence()
    {
        var start = Local(Utc, 2026, 1, 1);
        var endsAt = RecurrenceRule.Parse("FREQ=DAILY;COUNT=5").ComputeEndsAt(start, Utc);
        Assert.Equal(Local(Utc, 2026, 1, 5), endsAt);
    }

    [Fact]
    public void Ends_at_is_the_until_bound()
    {
        var start = Local(Utc, 2026, 1, 1);
        var endsAt = RecurrenceRule.Parse("FREQ=DAILY;UNTIL=20260110T090000Z").ComputeEndsAt(start, Utc);
        Assert.Equal(Local(Utc, 2026, 1, 10), endsAt);
    }

    // ── DST: the reason the engine expands on the wall clock ──

    [Fact]
    public void Weekday_reminder_fires_once_per_day_across_spring_forward()
    {
        // "Every weekday at 08:00" in New York, spanning the 2026-03-08 spring-forward (02:00→03:00).
        var start = Local(NewYork, 2026, 3, 2, hour: 8); // Monday
        var occ = Expand("FREQ=WEEKLY;BYDAY=MO,TU,WE,TH,FR",
            start, start, Local(NewYork, 2026, 3, 13, hour: 8), NewYork);

        // Ten business days, one apiece — the weekend is skipped and nothing is doubled or dropped.
        Assert.Equal(10, occ.Count);
        Assert.All(occ, o =>
        {
            var local = TimeZoneInfo.ConvertTime(o, NewYork);
            Assert.Equal(new TimeSpan(8, 0, 0), local.TimeOfDay);                 // still 08:00 on the wall
            Assert.DoesNotContain(local.DayOfWeek, new[] { DayOfWeek.Saturday, DayOfWeek.Sunday });
        });

        // The wall clock held at 08:00, so the UTC instant moved by an hour once EDT began.
        Assert.Equal(13, occ.First().UtcDateTime.Hour); // 08:00 EST = 13:00 UTC (before the change)
        Assert.Equal(12, occ.Last().UtcDateTime.Hour);  // 08:00 EDT = 12:00 UTC (after the change)

        // Exactly one occurrence per calendar day.
        Assert.Equal(occ.Count, occ.Select(o => TimeZoneInfo.ConvertTime(o, NewYork).Date).Distinct().Count());
    }

    [Fact]
    public void Daily_reminder_fires_once_per_day_across_fall_back()
    {
        // The 2026-11-01 fall-back (02:00→01:00) repeats an hour; a daily 08:00 reminder must not double.
        var start = Local(NewYork, 2026, 10, 30, hour: 8);
        var occ = Expand("FREQ=DAILY", start, start, Local(NewYork, 2026, 11, 3, hour: 8), NewYork);

        Assert.Equal(5, occ.Count);
        Assert.Equal(occ.Count, occ.Select(o => TimeZoneInfo.ConvertTime(o, NewYork).Date).Distinct().Count());
        Assert.All(occ, o => Assert.Equal(new TimeSpan(8, 0, 0), TimeZoneInfo.ConvertTime(o, NewYork).TimeOfDay));
    }
}
