using Pottmayer.Pandora.Modules.Agenda.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Agenda.Domain.Recurrence;
using Pottmayer.Pandora.Modules.Agenda.Domain.ValueObjects;
using Xunit;

namespace Pottmayer.Pandora.Modules.Agenda.Tests;

/// <summary>
/// All-day values are dates: stored at the item zone's midnight, read back at the viewer zone's midnight,
/// so "Nov 14" stays Nov 14 for a viewer in any zone.
/// </summary>
public sealed class AllDayDatesTests
{
    private static readonly TimeZoneInfo SaoPaulo = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
    private static readonly TimeZoneInfo Tokyo = TimeZoneInfo.FindSystemTimeZoneById("Asia/Tokyo");
    private static readonly TimeZoneInfo LosAngeles = TimeZoneInfo.FindSystemTimeZoneById("America/Los_Angeles");
    private static readonly TimeZoneInfo Kiritimati = TimeZoneInfo.FindSystemTimeZoneById("Pacific/Kiritimati"); // UTC+14
    private static readonly TimeProvider Time = new FixedTimeProvider(new DateTimeOffset(2026, 11, 1, 12, 0, 0, TimeSpan.Zero));
    private static readonly DateOnly Nov14 = new(2026, 11, 14);

    private static DateTimeOffset At(int day, int hour, int minute, double offsetHours) =>
        new(2026, 11, day, hour, minute, 0, TimeSpan.FromHours(offsetHours));

    // ── reading the date the caller meant ──

    [Fact]
    public void A_value_with_an_offset_is_read_on_its_own_clock_whatever_the_item_zone()
    {
        // 23:30 in São Paulo is already Nov 15 in Tokyo; the caller meant Nov 14.
        Assert.Equal(Nov14, AllDayDates.DateOf(At(14, 23, 30, -3), Tokyo));
        // 00:30 in Tokyo is still Nov 13 in Los Angeles.
        Assert.Equal(Nov14, AllDayDates.DateOf(At(14, 0, 30, 9), LosAngeles));
    }

    [Fact]
    public void A_utc_value_is_read_in_the_item_zone()
    {
        // How a stored value comes back from the database: Nov 14 00:00 Tokyo is Nov 13 15:00 UTC.
        Assert.Equal(Nov14, AllDayDates.DateOf(new DateTimeOffset(2026, 11, 13, 15, 0, 0, TimeSpan.Zero), Tokyo));
    }

    [Fact]
    public void StartOf_is_that_zones_midnight()
    {
        Assert.Equal(At(14, 0, 0, -3), AllDayDates.StartOf(Nov14, SaoPaulo));
        Assert.Equal(At(14, 0, 0, 14), AllDayDates.StartOf(Nov14, Kiritimati));
    }

    [Fact]
    public void StartOf_a_day_whose_midnight_dst_skips_is_the_first_wall_time_that_exists()
    {
        // Cuba springs forward at midnight: 2026-03-08 00:00 does not exist there.
        var havana = TimeZoneInfo.FindSystemTimeZoneById("America/Havana");
        var day = new DateOnly(2026, 3, 8);

        var start = AllDayDates.StartOf(day, havana);

        Assert.Equal(day, DateOnly.FromDateTime(start.DateTime));
        Assert.False(havana.IsInvalidTime(start.DateTime));
        Assert.Equal(day, AllDayDates.DateOf(start.ToUniversalTime(), havana)); // and it reads back as that day
    }

    // ── spans ──

    [Fact]
    public void A_span_ending_at_a_midnight_keeps_it_as_the_exclusive_end()
    {
        var (start, end) = AllDayDates.SnapSpan(At(14, 0, 0, -3), At(16, 0, 0, -3), SaoPaulo);

        Assert.Equal(At(14, 0, 0, -3), start);
        Assert.Equal(At(16, 0, 0, -3), end); // Nov 14 and 15
    }

    [Fact]
    public void A_span_ending_during_a_day_takes_that_day_whole()
    {
        // The web form sends the range it shows: 10:00 to 11:00 on Nov 14.
        var (start, end) = AllDayDates.SnapSpan(At(14, 10, 0, -3), At(14, 11, 0, -3), SaoPaulo);

        Assert.Equal(At(14, 0, 0, -3), start);
        Assert.Equal(At(15, 0, 0, -3), end);
    }

    [Fact]
    public void A_zero_length_span_is_one_day()
    {
        var (start, end) = AllDayDates.SnapSpan(At(14, 0, 0, -3), At(14, 0, 0, -3), SaoPaulo);
        Assert.Equal(TimeSpan.FromDays(1), end - start);
    }

    [Fact]
    public void A_stored_span_snaps_to_itself()
    {
        var (start, end) = AllDayDates.SnapSpan(At(14, 0, 0, 9), At(16, 0, 0, 9), Tokyo);

        var again = AllDayDates.SnapSpan(start, end, Tokyo); // UTC now, as the database returns it

        Assert.Equal((start, end), again);
    }

    [Fact]
    public void Reanchor_keeps_the_dates_in_the_viewer_zone()
    {
        var (start, end) = AllDayDates.SnapSpan(At(14, 0, 0, 9), At(16, 0, 0, 9), Tokyo);

        var (viewStart, viewEnd) = AllDayDates.Reanchor(start, end, Tokyo, SaoPaulo);

        Assert.Equal(At(14, 0, 0, -3), viewStart);
        Assert.Equal(At(16, 0, 0, -3), viewEnd);
    }

    [Fact]
    public void Reanchor_counts_whole_days_across_a_dst_change()
    {
        // Los Angeles falls back on Nov 1: that day lasts 25 hours.
        var (start, end) = AllDayDates.SnapSpan(At(1, 0, 0, -7), At(2, 0, 0, -8), LosAngeles);
        Assert.Equal(TimeSpan.FromHours(25), end - start);

        var (viewStart, viewEnd) = AllDayDates.Reanchor(start, end, LosAngeles, SaoPaulo);

        Assert.Equal(At(1, 0, 0, -3), viewStart);
        Assert.Equal(At(2, 0, 0, -3), viewEnd);
    }

    // ── tasks ──

    [Fact]
    public void A_date_only_task_is_due_at_the_start_of_its_date_whatever_clock_time_the_picker_sent()
    {
        var task = TaskItem.Create(Guid.NewGuid(), Guid.NewGuid(), "Pay rent", null, At(14, 23, 30, -3),
            dueHasTime: false, TaskPriority.None, "America/Sao_Paulo", null, 0, Time);

        Assert.Equal(At(14, 0, 0, -3), task.DueAt);
        Assert.Equal(Nov14, task.LocalDueDate());
    }

    [Fact]
    public void A_timed_task_keeps_its_instant()
    {
        var task = TaskItem.Create(Guid.NewGuid(), Guid.NewGuid(), "Call", null, At(14, 23, 30, -3),
            dueHasTime: true, TaskPriority.None, "America/Sao_Paulo", null, 0, Time);

        Assert.Equal(At(14, 23, 30, -3), task.DueAt);
        Assert.Equal(TimeSpan.Zero, task.DueAt!.Value.Offset);
        Assert.Equal(Nov14, task.LocalDueDate());
    }

    [Fact]
    public void Updating_a_date_only_task_with_its_stored_due_keeps_the_date()
    {
        // A Tokyo task's Nov 14 is stored as Nov 13 15:00 UTC; saving it back unchanged must not move it.
        var task = TaskItem.Create(Guid.NewGuid(), Guid.NewGuid(), "Pay rent", null, At(14, 0, 0, 9),
            dueHasTime: false, TaskPriority.None, "Asia/Tokyo", null, 0, Time);

        task.Update(task.Title, null, task.DueAt, dueHasTime: false, TaskPriority.None);
        task.Update(task.Title, null, task.DueAt!.Value.ToUniversalTime(), dueHasTime: false, TaskPriority.None);

        Assert.Equal(Nov14, task.LocalDueDate());
    }

    [Fact]
    public void A_date_from_a_viewer_in_another_zone_lands_on_that_date()
    {
        // The task lives in Tokyo; a viewer in Los Angeles picks Nov 14 in their own zone.
        var task = TaskItem.Create(Guid.NewGuid(), Guid.NewGuid(), "Pay rent", null, null,
            dueHasTime: false, TaskPriority.None, "Asia/Tokyo", null, 0, Time);

        task.Update(task.Title, null, At(14, 0, 0, -8), dueHasTime: false, TaskPriority.None);

        Assert.Equal(Nov14, task.LocalDueDate());
    }

    [Fact]
    public void A_recurring_date_only_task_spawns_on_the_next_date()
    {
        var task = TaskItem.Create(Guid.NewGuid(), Guid.NewGuid(), "Water plants", null, At(14, 18, 0, -3),
            dueHasTime: false, TaskPriority.None, "America/Sao_Paulo", "FREQ=DAILY", 0, Time);

        var next = task.ComputeNextDueAt();

        Assert.Equal(At(15, 0, 0, -3), next);
    }

    // ── events ──

    private static Event AllDay(DateTimeOffset start, DateTimeOffset end, string zone, string? rrule = null) =>
        Event.Create(Guid.NewGuid(), Guid.NewGuid(), "Holiday", null, null, null, start, end,
            isAllDay: true, zone, rrule, EventStatus.Confirmed, Time);

    [Fact]
    public void An_all_day_event_snaps_to_whole_days_in_its_zone()
    {
        var ev = AllDay(At(14, 10, 0, -3), At(14, 11, 0, -3), "America/Sao_Paulo");

        Assert.Equal(At(14, 0, 0, -3), ev.StartsAt);
        Assert.Equal(At(15, 0, 0, -3), ev.EndsAt);
    }

    [Fact]
    public void Updating_an_all_day_event_with_its_stored_span_keeps_it()
    {
        var ev = AllDay(At(14, 0, 0, 9), At(16, 0, 0, 9), "Asia/Tokyo");
        var (start, end) = (ev.StartsAt, ev.EndsAt);

        ev.Update(ev.Title, null, null, null, ev.StartsAt, ev.EndsAt, isAllDay: true, ev.CalendarId);

        Assert.Equal((start, end), (ev.StartsAt, ev.EndsAt));
    }

    [Fact]
    public void A_timed_event_turned_all_day_covers_its_day()
    {
        var ev = Event.Create(Guid.NewGuid(), Guid.NewGuid(), "Trip", null, null, null, At(14, 9, 0, -3), At(14, 18, 0, -3),
            isAllDay: false, "America/Sao_Paulo", null, EventStatus.Confirmed, Time);

        ev.Update(ev.Title, null, null, null, ev.StartsAt, ev.EndsAt, isAllDay: true, ev.CalendarId);

        Assert.Equal(At(14, 0, 0, -3), ev.StartsAt);
        Assert.Equal(At(15, 0, 0, -3), ev.EndsAt);
    }

    [Fact]
    public void Moving_one_all_day_occurrence_snaps_it_too()
    {
        var ev = AllDay(At(14, 0, 0, -3), At(15, 0, 0, -3), "America/Sao_Paulo", "FREQ=WEEKLY");

        var (start, end) = ev.NormalizeOccurrence(At(22, 15, 0, -3), At(22, 16, 0, -3));

        Assert.Equal(At(22, 0, 0, -3), start);
        Assert.Equal(At(23, 0, 0, -3), end);
    }

    [Fact]
    public void A_viewer_in_another_zone_sees_an_all_day_event_on_its_own_date_only()
    {
        var ev = AllDay(At(14, 0, 0, 9), At(15, 0, 0, 9), "Asia/Tokyo");

        // São Paulo's Nov 13 — the stored instant (Nov 13 15:00 UTC) falls inside it, the date does not.
        var nov13 = EventExpander.Expand(ev, [], At(13, 0, 0, -3), At(13, 23, 59, -3), SaoPaulo);
        var nov14 = Assert.Single(EventExpander.Expand(ev, [], At(14, 0, 0, -3), At(14, 23, 59, -3), SaoPaulo));

        Assert.Empty(nov13);
        Assert.Equal(At(14, 0, 0, -3), nov14.StartsAt);
        Assert.Equal(At(15, 0, 0, -3), nov14.EndsAt);
        Assert.Equal(ev.StartsAt, nov14.OriginalStartsAt); // the edit key stays the stored start
    }

    [Fact]
    public void An_all_day_event_does_not_spill_into_the_next_day_for_a_viewer_in_its_own_zone()
    {
        var ev = AllDay(At(14, 0, 0, -3), At(15, 0, 0, -3), "America/Sao_Paulo");

        Assert.Empty(EventExpander.Expand(ev, [], At(15, 0, 0, -3), At(15, 23, 59, -3), SaoPaulo));
    }

    [Fact]
    public void A_recurring_all_day_event_keeps_each_date_for_a_viewer_far_away()
    {
        var ev = AllDay(At(14, 0, 0, 9), At(15, 0, 0, 9), "Asia/Tokyo", "FREQ=WEEKLY");

        var occurrences = EventExpander.Expand(ev, [], At(1, 0, 0, -8), At(30, 23, 59, -8), LosAngeles);

        Assert.Equal(
            [new DateOnly(2026, 11, 14), new DateOnly(2026, 11, 21), new DateOnly(2026, 11, 28)],
            occurrences.Select(o => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(o.StartsAt, LosAngeles).DateTime)));
        Assert.All(occurrences, o => Assert.Equal(TimeSpan.Zero, TimeZoneInfo.ConvertTime(o.StartsAt, LosAngeles).TimeOfDay));
    }

    [Fact]
    public void Without_a_viewer_zone_an_all_day_event_reads_as_stored()
    {
        var ev = AllDay(At(14, 0, 0, 9), At(15, 0, 0, 9), "Asia/Tokyo");

        var occ = Assert.Single(EventExpander.Expand(ev, [], At(14, 0, 0, 9), At(14, 23, 59, 9)));

        Assert.Equal(ev.StartsAt, occ.StartsAt);
    }
}
