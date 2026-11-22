using Pottmayer.Pandora.Modules.Agenda.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Agenda.Domain.ValueObjects;
using Xunit;

namespace Pottmayer.Pandora.Modules.Agenda.Tests;

public sealed class TaskTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeProvider Time = new FixedTimeProvider(Now);
    private static readonly Guid User = Guid.NewGuid();
    private static readonly Guid List = Guid.NewGuid();

    private static TaskItem Top(
        DateTimeOffset? dueAt = null, string? rrule = null, string zone = "UTC") =>
        TaskItem.Create(User, List, "Pay the bill", "note", dueAt, dueHasTime: true, TaskPriority.Medium, zone, rrule, 0, Time);

    // ── creation ──

    [Fact]
    public void Create_starts_todo_with_its_fields()
    {
        var task = Top(dueAt: Now.AddDays(1));
        Assert.Equal(TaskItemStatus.Todo, task.Status);
        Assert.Equal(List, task.ListId);
        Assert.Null(task.ParentTaskId);
        Assert.Equal(TaskPriority.Medium, task.Priority);
        Assert.False(task.IsRecurring);
    }

    [Fact]
    public void Create_requires_a_title()
        => Assert.Throws<ArgumentException>(() =>
            TaskItem.Create(User, List, "  ", null, null, false, TaskPriority.None, "UTC", null, 0, Time));

    [Fact]
    public void A_recurring_task_needs_a_due_date()
        => Assert.Throws<InvalidOperationException>(() => Top(dueAt: null, rrule: "FREQ=DAILY"));

    [Fact]
    public void An_unsupported_rrule_is_rejected()
        => Assert.Throws<FormatException>(() => Top(dueAt: Now, rrule: "FREQ=DAILY;BYSETPOS=1"));

    [Fact]
    public void Count_is_rejected_for_a_recurring_task()
        => Assert.Throws<FormatException>(() => Top(dueAt: Now, rrule: "FREQ=DAILY;COUNT=5"));

    // ── subtasks: one level, enforced in the aggregate ──

    [Fact]
    public void A_subtask_inherits_its_parents_list_and_points_at_it()
    {
        var parent = Top(dueAt: Now.AddDays(1));
        var sub = TaskItem.CreateSubtask(parent, "Step 1", null, null, false, TaskPriority.None, 0, Time);

        Assert.Equal(parent.Id, sub.ParentTaskId);
        Assert.Equal(parent.ListId, sub.ListId);
        Assert.True(sub.IsSubtask);
    }

    [Fact]
    public void A_subtask_cannot_have_subtasks()
    {
        var parent = Top();
        var sub = TaskItem.CreateSubtask(parent, "Step 1", null, null, false, TaskPriority.None, 0, Time);
        Assert.Throws<InvalidOperationException>(() =>
            TaskItem.CreateSubtask(sub, "Step 1.1", null, null, false, TaskPriority.None, 0, Time));
    }

    // ── complete / reopen ──

    [Fact]
    public void Complete_stamps_done_and_is_idempotent()
    {
        var task = Top(dueAt: Now);
        task.Complete(Time);
        Assert.Equal(TaskItemStatus.Done, task.Status);
        Assert.Equal(Now, task.CompletedAt);

        task.Complete(new FixedTimeProvider(Now.AddHours(2)));
        Assert.Equal(Now, task.CompletedAt); // unchanged
    }

    [Fact]
    public void Reopen_clears_the_completion_stamp()
    {
        var task = Top(dueAt: Now);
        task.Complete(Time);
        task.Reopen();

        Assert.Equal(TaskItemStatus.Todo, task.Status);
        Assert.Null(task.CompletedAt);
    }

    // ── recurrence: next occurrence + spawn ──

    [Fact]
    public void ComputeNextDueAt_is_null_for_a_non_recurring_task()
        => Assert.Null(Top(dueAt: Now).ComputeNextDueAt());

    [Theory]
    [InlineData("FREQ=DAILY", 1)]
    [InlineData("FREQ=WEEKLY;BYDAY=TH", 7)] // 2026-01-01 is a Thursday
    public void ComputeNextDueAt_returns_the_next_occurrence(string rrule, int daysAhead)
    {
        var due = new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero);
        var task = TaskItem.Create(User, List, "Recurring", null, due, true, TaskPriority.None, "UTC", rrule, 0, Time);

        Assert.Equal(due.AddDays(daysAhead), task.ComputeNextDueAt());
    }

    [Fact]
    public void A_bounded_series_stops_after_its_until()
    {
        var due = new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero);
        var task = TaskItem.Create(
            User, List, "Two days", null, due, true, TaskPriority.None, "UTC", "FREQ=DAILY;UNTIL=20260101T235959Z", 0, Time);

        // The only occurrence on/before UNTIL is the anchor itself; there is no next.
        Assert.Null(task.ComputeNextDueAt());
    }

    [Fact]
    public void SpawnNext_copies_the_series_onto_a_fresh_todo_row()
    {
        var due = new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero);
        var task = TaskItem.Create(User, List, "Recurring", "keep me", due, true, TaskPriority.High, "UTC", "FREQ=DAILY", 0, Time);

        var next = task.ComputeNextDueAt()!.Value;
        var spawned = task.SpawnNext(next, Time);

        Assert.NotEqual(task.Id, spawned.Id);
        Assert.Equal(TaskItemStatus.Todo, spawned.Status);
        Assert.Null(spawned.CompletedAt);
        Assert.Equal(next, spawned.DueAt);
        Assert.Equal("keep me", spawned.Notes);
        Assert.Equal(TaskPriority.High, spawned.Priority);
        Assert.Equal(task.Rrule, spawned.Rrule);
        Assert.Equal(task.ListId, spawned.ListId);
    }

    [Fact]
    public void The_next_occurrence_keeps_the_wall_clock_time_across_a_spring_forward()
    {
        // America/New_York springs forward on 2026-03-08 at 02:00. A daily task at 09:00 local must stay
        // at 09:00 local either side of the transition — its UTC instant shifts by the DST hour.
        var ny = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
        var localBefore = new DateTime(2026, 3, 7, 9, 0, 0);
        var due = new DateTimeOffset(localBefore, ny.GetUtcOffset(localBefore));

        var task = TaskItem.Create(User, List, "Meds", null, due, true, TaskPriority.None, "America/New_York", "FREQ=DAILY", 0, Time);

        var next = task.ComputeNextDueAt()!.Value;
        var nextLocal = TimeZoneInfo.ConvertTime(next, ny);

        Assert.Equal(new DateTime(2026, 3, 8, 9, 0, 0), nextLocal.DateTime);
        // Spring-forward: 09:00 EST → 09:00 EDT is 23 hours of real time, not a naive 24.
        Assert.Equal(due.ToUniversalTime().AddHours(23), next.ToUniversalTime());
    }
}
