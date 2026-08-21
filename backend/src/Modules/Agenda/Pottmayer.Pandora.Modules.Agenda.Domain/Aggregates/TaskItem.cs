using Pottmayer.Pandora.Modules.Agenda.Domain.Recurrence;
using Pottmayer.Pandora.Modules.Agenda.Domain.ValueObjects;
using Pottmayer.Pandora.Shared.Domain;
using Pottmayer.Tars.Core.Ddd;

namespace Pottmayer.Pandora.Modules.Agenda.Domain.Aggregates;

/// <summary>
/// Something to do: status, priority, an optional due date, optional one-level subtasks and optional
/// recurrence. Mapped to <c>agd005_task</c>; named <c>TaskItem</c> to avoid clashing with
/// <see cref="System.Threading.Tasks.Task"/>.
///
/// <para><b>Recurrence is materialized one instance at a time</b> (unlike a reminder, which keeps one
/// row and expands occurrences lazily). Each recurring task is a concrete row with a concrete
/// <see cref="DueAt"/>; completing it closes this instance and the application inserts the next one
/// (<see cref="ComputeNextDueAt"/> + <see cref="SpawnNext"/>), carrying its fields and alerts. Two
/// rows, not one mutable row, so history survives.</para>
/// </summary>
public sealed class TaskItem : AggregateRoot<Guid>, IAuditable
{
    private const int MaxTitleLength = 200;

    // How far ahead the next occurrence is searched for. Every supported non-COUNT rule places the
    // next occurrence at most a few INTERVAL periods away, so this only ever bounds a truly dead series.
    private static readonly TimeSpan NextOccurrenceHorizon = TimeSpan.FromDays(3660);

    public Guid UserId { get; private set; }
    public Guid ListId { get; private set; }

    /// <summary>Set on a subtask; null on a top-level task. Depth is limited to one level.</summary>
    public Guid? ParentTaskId { get; private set; }

    public string Title { get; private set; } = string.Empty;
    public string? Notes { get; private set; }

    /// <summary>When it is due. Null ⇒ no due date. Required when <see cref="Rrule"/> is set.</summary>
    public DateTimeOffset? DueAt { get; private set; }

    /// <summary>Whether <see cref="DueAt"/> carries a meaningful time of day; false ⇒ a whole-day due date.</summary>
    public bool DueHasTime { get; private set; }

    public TaskPriority Priority { get; private set; }
    public TaskItemStatus Status { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }

    /// <summary>IANA time zone the recurrence is expanded in. Defaults to UTC until Identity carries a user default.</summary>
    public string TimeZone { get; private set; } = "UTC";

    /// <summary>Raw RRULE (RFC 5545 subset), stored verbatim. Null ⇒ not recurring. Only on top-level tasks.</summary>
    public string? Rrule { get; private set; }

    public int Position { get; private set; }

    /// <summary>Soft delete.</summary>
    public DateTimeOffset? DeletedAt { get; private set; }

    public Guid? CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }

    private TaskItem() { }

    public bool IsRecurring => Rrule is not null;
    public bool IsSubtask => ParentTaskId is not null;

    /// <summary>Creates a top-level task. A recurring task requires a due date (its recurrence anchor).</summary>
    public static TaskItem Create(
        Guid userId,
        Guid listId,
        string title,
        string? notes,
        DateTimeOffset? dueAt,
        bool dueHasTime,
        TaskPriority priority,
        string timeZone,
        string? rrule,
        int position,
        TimeProvider timeProvider)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("A task needs a title.", nameof(title));

        var zone = string.IsNullOrWhiteSpace(timeZone) ? "UTC" : timeZone;
        dueAt = dueAt?.ToUniversalTime();
        var storedRrule = NormalizeRrule(rrule, dueAt, zone);

        return new TaskItem
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            ListId = listId,
            ParentTaskId = null,
            Title = TrimTitle(title),
            Notes = notes,
            DueAt = dueAt,
            DueHasTime = dueHasTime,
            Priority = priority,
            Status = TaskItemStatus.Todo,
            TimeZone = zone,
            Rrule = storedRrule,
            Position = position,
            CreatedAt = timeProvider.GetUtcNow()
        };
    }

    /// <summary>
    /// Creates a subtask under <paramref name="parent"/>. Enforces the one-level rule in the aggregate:
    /// the parent must itself be top-level. A subtask inherits the parent's list and cannot recur.
    /// </summary>
    public static TaskItem CreateSubtask(
        TaskItem parent,
        string title,
        string? notes,
        DateTimeOffset? dueAt,
        bool dueHasTime,
        TaskPriority priority,
        int position,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(parent);
        if (parent.IsSubtask)
            throw new InvalidOperationException("Subtasks are limited to one level; a subtask cannot have subtasks.");
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("A task needs a title.", nameof(title));

        return new TaskItem
        {
            Id = Guid.CreateVersion7(),
            UserId = parent.UserId,
            ListId = parent.ListId,
            ParentTaskId = parent.Id,
            Title = TrimTitle(title),
            Notes = notes,
            DueAt = dueAt?.ToUniversalTime(),
            DueHasTime = dueHasTime,
            Priority = priority,
            Status = TaskItemStatus.Todo,
            TimeZone = parent.TimeZone,
            Rrule = null,
            Position = position,
            CreatedAt = timeProvider.GetUtcNow()
        };
    }

    public void Update(string title, string? notes, DateTimeOffset? dueAt, bool dueHasTime, TaskPriority priority)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("A task needs a title.", nameof(title));

        // A recurring task keeps its anchor: it cannot lose the due date its recurrence hangs on.
        if (IsRecurring && dueAt is null)
            throw new InvalidOperationException("A recurring task must keep a due date.");

        Title = TrimTitle(title);
        Notes = notes;
        DueAt = dueAt?.ToUniversalTime();
        DueHasTime = dueHasTime;
        Priority = priority;
    }

    public void SetPosition(int position) => Position = position;

    /// <summary>Marks the task done. Idempotent: completing an already-done task is a no-op.</summary>
    public void Complete(TimeProvider timeProvider)
    {
        if (Status == TaskItemStatus.Done)
            return;

        Status = TaskItemStatus.Done;
        CompletedAt = timeProvider.GetUtcNow();
    }

    /// <summary>Reopens the task, clearing the completion stamp. Idempotent for a non-completed task.</summary>
    public void Reopen()
    {
        Status = TaskItemStatus.Todo;
        CompletedAt = null;
    }

    /// <summary>Soft-deletes the task.</summary>
    public void Delete(TimeProvider timeProvider) => DeletedAt ??= timeProvider.GetUtcNow();

    /// <summary>
    /// The next occurrence strictly after <see cref="DueAt"/>, or null when the task is not recurring,
    /// has no due date, or its series has ended (past <c>UNTIL</c>). Re-anchoring at the current due
    /// date is correct because a materialized instance's due date is always an on-grid occurrence.
    /// </summary>
    public DateTimeOffset? ComputeNextDueAt()
    {
        if (!IsRecurring || DueAt is not { } due)
            return null;

        var rule = RecurrenceRule.Parse(Rrule!);
        var zone = ResolveZone();
        foreach (var occurrence in rule.Expand(due, due.AddTicks(1), due + NextOccurrenceHorizon, zone))
            return occurrence;

        return null;
    }

    /// <summary>
    /// The next instance of a recurring series: a fresh <c>Todo</c> row that copies list, title, notes,
    /// priority, zone and rule, anchored at <paramref name="nextDueAt"/>. Alerts are carried by the
    /// application (they are a sibling aggregate).
    /// </summary>
    public TaskItem SpawnNext(DateTimeOffset nextDueAt, TimeProvider timeProvider) => new()
    {
        Id = Guid.CreateVersion7(),
        UserId = UserId,
        ListId = ListId,
        ParentTaskId = null,
        Title = Title,
        Notes = Notes,
        DueAt = nextDueAt,
        DueHasTime = DueHasTime,
        Priority = Priority,
        Status = TaskItemStatus.Todo,
        TimeZone = TimeZone,
        Rrule = Rrule,
        Position = Position,
        CreatedAt = timeProvider.GetUtcNow()
    };

    /// <summary>The task's zone as a <see cref="TimeZoneInfo"/>, throwing on an unknown IANA id.</summary>
    public TimeZoneInfo ResolveZone() => ResolveZone(TimeZone);

    private static TimeZoneInfo ResolveZone(string timeZone)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZone);
        }
        catch (TimeZoneNotFoundException)
        {
            throw new ArgumentException($"Unknown IANA time zone '{timeZone}'.", nameof(timeZone));
        }
    }

    private static string? NormalizeRrule(string? rrule, DateTimeOffset? dueAt, string zone)
    {
        if (string.IsNullOrWhiteSpace(rrule))
            return null;

        if (dueAt is null)
            throw new InvalidOperationException("A recurring task needs a due date to anchor its recurrence.");

        // Parse rejects anything outside the supported subset — the write-time guard.
        var rule = RecurrenceRule.Parse(rrule);

        // COUNT cannot be honored by a one-instance-at-a-time model (no occurrence index survives across
        // rows). UNTIL and open-ended rules are fully supported. See phase-3 notes.
        if (rule.Count is not null)
            throw new FormatException("COUNT is not supported for recurring tasks; use UNTIL or an open-ended rule.");

        // Validate the zone eagerly, same as expansion will use it.
        ResolveZone(zone);
        return rule.Raw;
    }

    private static string TrimTitle(string title) =>
        title.Length <= MaxTitleLength ? title.Trim() : title[..MaxTitleLength].Trim();
}
