using Pottmayer.Pandora.Modules.Agenda.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Agenda.Domain.ValueObjects;
using Xunit;

namespace Pottmayer.Pandora.Modules.Agenda.Tests;

public sealed class ReminderTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeProvider Time = new FixedTimeProvider(Now);

    private static Reminder At(DateTimeOffset remindAt) =>
        Reminder.Create(Guid.NewGuid(), "Pay the bill", null, remindAt, "UTC", Time);

    [Fact]
    public void Create_starts_scheduled()
    {
        var reminder = At(Now.AddHours(1));
        Assert.Equal(ReminderStatus.Scheduled, reminder.Status);
        Assert.Equal(Now.AddHours(1), reminder.RemindAt);
    }

    [Fact]
    public void Create_requires_a_title()
        => Assert.Throws<ArgumentException>(() => Reminder.Create(Guid.NewGuid(), "  ", null, Now, "UTC", Time));

    [Fact]
    public void Is_due_when_scheduled_and_past()
    {
        Assert.True(At(Now.AddMinutes(-1)).IsDue(Now));
        Assert.False(At(Now.AddMinutes(1)).IsDue(Now));
    }

    [Fact]
    public void Notifying_stops_it_being_due_again()
    {
        var reminder = At(Now.AddMinutes(-1));
        reminder.MarkNotified();
        Assert.Equal(ReminderStatus.Notified, reminder.Status);
        Assert.False(reminder.IsDue(Now));
    }

    [Fact]
    public void Snooze_defers_the_effective_time()
    {
        var reminder = At(Now.AddMinutes(-1));
        reminder.MarkNotified();
        reminder.Snooze(Now.AddHours(1));

        Assert.Equal(ReminderStatus.Snoozed, reminder.Status);
        Assert.False(reminder.IsDue(Now));               // pushed out an hour
        Assert.True(reminder.IsDue(Now.AddHours(2)));     // due once the snooze passes
    }

    [Fact]
    public void Acknowledge_is_terminal_so_a_later_snooze_is_a_no_op()
    {
        var reminder = At(Now.AddMinutes(-1));
        reminder.Acknowledge(Time);
        reminder.Snooze(Now.AddHours(1));

        Assert.Equal(ReminderStatus.Acknowledged, reminder.Status);
        Assert.Equal(Now, reminder.AcknowledgedAt);
    }

    [Fact]
    public void Cancel_stops_it_firing()
    {
        var reminder = At(Now.AddMinutes(-1));
        reminder.Cancel();
        Assert.Equal(ReminderStatus.Cancelled, reminder.Status);
        Assert.False(reminder.IsDue(Now));
    }
}
