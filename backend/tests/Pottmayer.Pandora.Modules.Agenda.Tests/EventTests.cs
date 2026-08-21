using Pottmayer.Pandora.Modules.Agenda.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Agenda.Domain.Recurrence;
using Pottmayer.Pandora.Modules.Agenda.Domain.ValueObjects;
using Xunit;

namespace Pottmayer.Pandora.Modules.Agenda.Tests;

public sealed class EventTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeProvider Time = new FixedTimeProvider(Now);
    private static readonly Guid User = Guid.NewGuid();
    private static readonly Guid Cal = Guid.NewGuid();

    private static Event Make(
        DateTimeOffset startsAt, TimeSpan duration, string? rrule = null, bool isAllDay = false, string zone = "UTC") =>
        Event.Create(
            User, Cal, "Standup", "notes", "Room 1", "https://meet", startsAt, startsAt + duration,
            isAllDay, zone, rrule, EventStatus.Confirmed, Time);

    private static EventOccurrenceOverride Override(Guid eventId, DateTimeOffset original) =>
        EventOccurrenceOverride.Create(eventId, User, original, Time);

    // ── creation ──

    [Fact]
    public void Create_rejects_an_empty_title() => Assert.Throws<ArgumentException>(() =>
        Event.Create(User, Cal, " ", null, null, null, Now, Now.AddHours(1), false, "UTC", null, EventStatus.Confirmed, Time));

    [Fact]
    public void Create_rejects_an_end_before_the_start() => Assert.Throws<ArgumentException>(() =>
        Event.Create(User, Cal, "X", null, null, null, Now, Now.AddHours(-1), false, "UTC", null, EventStatus.Confirmed, Time));

    [Fact]
    public void Create_rejects_an_unsupported_rrule() => Assert.Throws<FormatException>(() =>
        Make(Now, TimeSpan.FromHours(1), "FREQ=DAILY;BYSETPOS=1"));

    // ── range expansion ──

    [Fact]
    public void A_single_event_yields_one_occurrence_when_it_overlaps_the_window()
    {
        var start = new DateTimeOffset(2026, 1, 5, 9, 0, 0, TimeSpan.Zero);
        var ev = Make(start, TimeSpan.FromHours(1));

        var occ = EventExpander.Expand(ev, [], new DateTimeOffset(2026, 1, 5, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 1, 5, 23, 59, 59, TimeSpan.Zero));

        Assert.Single(occ);
        Assert.Equal(start, occ[0].StartsAt);
        Assert.Equal(start.AddHours(1), occ[0].EndsAt);
        Assert.Equal(start, occ[0].OriginalStartsAt);
    }

    [Fact]
    public void A_single_event_outside_the_window_yields_nothing()
    {
        var ev = Make(new DateTimeOffset(2026, 2, 1, 9, 0, 0, TimeSpan.Zero), TimeSpan.FromHours(1));
        var occ = EventExpander.Expand(ev, [], new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero));
        Assert.Empty(occ);
    }

    [Fact]
    public void A_daily_event_expands_one_occurrence_per_day_in_the_window()
    {
        var start = new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero);
        var ev = Make(start, TimeSpan.FromHours(1), "FREQ=DAILY");

        var occ = EventExpander.Expand(ev, [], new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 1, 3, 23, 59, 59, TimeSpan.Zero));

        Assert.Equal(3, occ.Count);
        Assert.Equal(start, occ[0].StartsAt);
        Assert.Equal(start.AddDays(1), occ[1].StartsAt);
        Assert.Equal(start.AddDays(2), occ[2].StartsAt);
    }

    [Fact]
    public void A_cancelled_occurrence_disappears_from_the_expansion()
    {
        var start = new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero);
        var ev = Make(start, TimeSpan.FromHours(1), "FREQ=DAILY");
        var ov = Override(ev.Id, start.AddDays(1));
        ov.Cancel();

        var occ = EventExpander.Expand(ev, [ov], new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 1, 3, 23, 59, 59, TimeSpan.Zero));

        Assert.Equal(2, occ.Count);
        Assert.DoesNotContain(occ, o => o.OriginalStartsAt == start.AddDays(1));
    }

    [Fact]
    public void An_edited_occurrence_overrides_only_that_one()
    {
        var start = new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero);
        var ev = Make(start, TimeSpan.FromHours(1), "FREQ=DAILY");
        var moved = start.AddDays(1).AddHours(2); // day 2 pushed to 11:00
        var ov = Override(ev.Id, start.AddDays(1));
        ov.Edit(moved, moved.AddMinutes(30), "Moved standup", null, "Room 2");

        var occ = EventExpander.Expand(ev, [ov], new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 1, 3, 23, 59, 59, TimeSpan.Zero));

        var edited = Assert.Single(occ, o => o.OriginalStartsAt == start.AddDays(1));
        Assert.Equal(moved, edited.StartsAt);
        Assert.Equal(moved.AddMinutes(30), edited.EndsAt);
        Assert.Equal("Moved standup", edited.Title);
        Assert.Equal("Room 2", edited.Location);
        // The other days keep the series values.
        Assert.Equal("Standup", occ.First(o => o.OriginalStartsAt == start).Title);
    }

    [Fact]
    public void An_all_day_event_keeps_its_flag_through_expansion()
    {
        var start = new DateTimeOffset(2026, 1, 10, 0, 0, 0, TimeSpan.Zero);
        var ev = Make(start, TimeSpan.FromDays(1), isAllDay: true);

        var occ = EventExpander.Expand(ev, [], new DateTimeOffset(2026, 1, 10, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 1, 10, 23, 59, 59, TimeSpan.Zero));

        Assert.Single(occ);
        Assert.True(occ[0].IsAllDay);
    }

    [Fact]
    public void Expansion_keeps_the_wall_clock_time_across_a_spring_forward()
    {
        // America/New_York springs forward 2026-03-08 02:00. A daily 09:00 event stays 09:00 local.
        var ny = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
        var localStart = new DateTime(2026, 3, 7, 9, 0, 0);
        var start = new DateTimeOffset(localStart, ny.GetUtcOffset(localStart));
        var ev = Make(start, TimeSpan.FromHours(1), "FREQ=DAILY", zone: "America/New_York");

        var occ = EventExpander.Expand(ev, [],
            new DateTimeOffset(2026, 3, 7, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 3, 9, 0, 0, 0, TimeSpan.Zero));

        Assert.Equal(2, occ.Count);
        foreach (var o in occ)
            Assert.Equal(new TimeSpan(9, 0, 0), TimeZoneInfo.ConvertTime(o.StartsAt, ny).TimeOfDay);
        // The UTC instants are 23 hours apart, not 24: the spring-forward hour is gone.
        Assert.Equal(occ[0].StartsAt.AddHours(23), occ[1].StartsAt);
    }

    // ── this-and-future split ──

    [Fact]
    public void EndSeriesBefore_stops_the_series_before_the_cut()
    {
        var start = new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero);
        var ev = Make(start, TimeSpan.FromHours(1), "FREQ=DAILY");

        ev.EndSeriesBefore(start.AddDays(2)); // cut at day 3 (Jan 3 09:00)

        Assert.Contains("UNTIL=", ev.Rrule);
        var occ = EventExpander.Expand(ev, [], new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 1, 10, 0, 0, 0, TimeSpan.Zero));
        Assert.Equal(2, occ.Count); // only Jan 1 and Jan 2 survive
        Assert.Equal(start.AddDays(1), occ[^1].StartsAt);
    }

    [Fact]
    public void EndSeriesBefore_is_rejected_on_a_single_event()
        => Assert.Throws<InvalidOperationException>(() =>
            Make(Now, TimeSpan.FromHours(1)).EndSeriesBefore(Now.AddDays(1)));

    // ── WithUntil (engine helper) ──

    [Fact]
    public void WithUntil_replaces_count_with_until()
    {
        var rule = RecurrenceRule.Parse("FREQ=WEEKLY;BYDAY=MO;COUNT=10");
        var bounded = rule.WithUntil(new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero));

        Assert.Contains("UNTIL=20260601T000000Z", bounded);
        Assert.DoesNotContain("COUNT", bounded);
        Assert.Contains("FREQ=WEEKLY", bounded);
        Assert.Contains("BYDAY=MO", bounded);
    }
}
