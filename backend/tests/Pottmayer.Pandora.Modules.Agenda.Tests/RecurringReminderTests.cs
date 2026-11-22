using Pottmayer.Pandora.Modules.Agenda.Application.Reminders;
using Pottmayer.Pandora.Modules.Agenda.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Agenda.Domain.ValueObjects;
using Xunit;

namespace Pottmayer.Pandora.Modules.Agenda.Tests;

/// <summary>
/// The recurrence-facing seams around the reminder: creating a recurring reminder, the per-occurrence
/// dispatch ledger row, and the button payload that tells single-shot and occurrence apart.
/// </summary>
public sealed class RecurringReminderTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeProvider Time = new FixedTimeProvider(Now);

    // ── Reminder.Create with an RRULE ──

    [Fact]
    public void A_reminder_without_an_rrule_is_single_shot()
    {
        var reminder = Reminder.Create(Guid.NewGuid(), "One-off", null, Now, "UTC", Time);
        Assert.False(reminder.IsRecurring);
        Assert.Null(reminder.Rrule);
        Assert.Null(reminder.RecurrenceEndsAt);
    }

    [Fact]
    public void A_reminder_with_an_rrule_is_recurring_and_stores_the_raw_string()
    {
        var reminder = Reminder.Create(
            Guid.NewGuid(), "Weekdays", null, Now, "America/New_York", Time, "FREQ=WEEKLY;BYDAY=MO,TU,WE,TH,FR");

        Assert.True(reminder.IsRecurring);
        Assert.Equal("FREQ=WEEKLY;BYDAY=MO,TU,WE,TH,FR", reminder.Rrule);
        Assert.Null(reminder.RecurrenceEndsAt); // open-ended
    }

    [Fact]
    public void A_bounded_rrule_computes_the_denormalized_end()
    {
        var start = new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero);
        var reminder = Reminder.Create(Guid.NewGuid(), "Five days", null, start, "UTC", Time, "FREQ=DAILY;COUNT=5");

        Assert.Equal(new DateTimeOffset(2026, 1, 5, 9, 0, 0, TimeSpan.Zero), reminder.RecurrenceEndsAt);
    }

    [Fact]
    public void An_unsupported_rrule_is_rejected_at_creation()
        => Assert.Throws<FormatException>(() =>
            Reminder.Create(Guid.NewGuid(), "Bad", null, Now, "UTC", Time, "FREQ=DAILY;BYSETPOS=1"));

    [Fact]
    public void An_unknown_time_zone_is_rejected_for_a_recurring_reminder()
        => Assert.Throws<ArgumentException>(() =>
            Reminder.Create(Guid.NewGuid(), "Bad zone", null, Now, "Mars/Olympus", Time, "FREQ=DAILY"));

    [Fact]
    public void A_recurring_reminder_stays_scheduled_and_cancel_stops_the_series()
    {
        var reminder = Reminder.Create(Guid.NewGuid(), "Daily", null, Now, "UTC", Time, "FREQ=DAILY");
        Assert.Equal(ReminderStatus.Scheduled, reminder.Status);

        reminder.Cancel();
        Assert.Equal(ReminderStatus.Cancelled, reminder.Status);
    }

    // ── ReminderDispatch: per-occurrence acknowledge/snooze ──

    private static ReminderDispatch Dispatch(DateTimeOffset occurrence) =>
        ReminderDispatch.Record(Guid.NewGuid(), Guid.NewGuid(), occurrence, Guid.NewGuid(), isLate: false, Time);

    [Fact]
    public void Acknowledging_an_occurrence_is_idempotent_and_clears_a_pending_snooze()
    {
        var dispatch = Dispatch(Now);
        dispatch.Snooze(Now.AddHours(1));
        dispatch.Acknowledge(Time);

        Assert.Equal(Now, dispatch.AcknowledgedAt);
        Assert.Null(dispatch.SnoozedUntil);

        // A second tap does not move the acknowledged time.
        dispatch.Acknowledge(new FixedTimeProvider(Now.AddHours(2)));
        Assert.Equal(Now, dispatch.AcknowledgedAt);
    }

    [Fact]
    public void A_snoozed_occurrence_becomes_re_fire_due_when_the_snooze_passes()
    {
        var dispatch = Dispatch(Now);
        dispatch.Snooze(Now.AddHours(1));

        Assert.False(dispatch.IsSnoozeDue(Now));
        Assert.True(dispatch.IsSnoozeDue(Now.AddHours(2)));

        dispatch.ClearSnooze();
        Assert.False(dispatch.IsSnoozeDue(Now.AddHours(2))); // one snooze, one re-fire
    }

    [Fact]
    public void Snoozing_an_acknowledged_occurrence_is_a_no_op()
    {
        var dispatch = Dispatch(Now);
        dispatch.Acknowledge(Time);
        dispatch.Snooze(Now.AddHours(1));

        Assert.Null(dispatch.SnoozedUntil);
        Assert.False(dispatch.IsSnoozeDue(Now.AddHours(2)));
    }

    // ── ReminderButtonPayload ──

    [Fact]
    public void Single_shot_payload_round_trips_to_just_the_reminder_id()
    {
        var reminderId = Guid.NewGuid();
        var payload = ReminderButtonPayload.ForReminder(reminderId);

        Assert.True(ReminderButtonPayload.TryParse(payload, out var parsedId, out var occurrence));
        Assert.Equal(reminderId, parsedId);
        Assert.Null(occurrence);
    }

    [Fact]
    public void Occurrence_payload_round_trips_to_reminder_and_occurrence()
    {
        var reminderId = Guid.NewGuid();
        var occ = new DateTimeOffset(2026, 3, 9, 12, 0, 0, TimeSpan.Zero);
        var payload = ReminderButtonPayload.ForOccurrence(reminderId, occ);

        Assert.True(ReminderButtonPayload.TryParse(payload, out var parsedId, out var parsedOcc));
        Assert.Equal(reminderId, parsedId);
        Assert.Equal(occ, parsedOcc);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-guid")]
    [InlineData("not-a-guid|123")]
    [InlineData("d7c3f6e2-0000-0000-0000-000000000000|not-a-number")]
    public void An_unparseable_payload_returns_false(string? payload)
        => Assert.False(ReminderButtonPayload.TryParse(payload, out _, out _));
}
