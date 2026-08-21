using Pottmayer.Pandora.Shared.Domain;
using Pottmayer.Tars.Core.Ddd;

namespace Pottmayer.Pandora.Modules.Agenda.Domain.Aggregates;

/// <summary>
/// The record that a reminder occurrence was dispatched. It is the idempotency key of the recurring
/// sweep: one row per <c>(reminder, occurrence_starts_at)</c>, so re-running a tick — or restarting
/// mid-tick — never double-fires an occurrence and never skips one.
///
/// <para>For a recurring reminder, acknowledge and snooze act on this row (the occurrence), not on the
/// series. Acknowledging stamps <see cref="AcknowledgedAt"/> and the series continues untouched.
/// Snoozing stamps <see cref="SnoozedUntil"/>; the sweep re-fires the occurrence once when that passes,
/// clearing it. An acknowledged occurrence never re-fires.</para>
/// </summary>
public sealed class ReminderDispatch : AggregateRoot<Guid>, IAuditable
{
    public Guid ReminderId { get; private set; }
    public Guid UserId { get; private set; }

    /// <summary>The occurrence anchor (UTC). Unique with <see cref="ReminderId"/>.</summary>
    public DateTimeOffset OccurrenceStartsAt { get; private set; }

    public DateTimeOffset DispatchedAt { get; private set; }

    /// <summary>The correlation id handed to Channels, so a delivery is traceable end to end.</summary>
    public Guid CorrelationId { get; private set; }

    /// <summary>Fired from the grace window rather than on its own tick. Informational.</summary>
    public bool IsLate { get; private set; }

    public DateTimeOffset? AcknowledgedAt { get; private set; }
    public DateTimeOffset? SnoozedUntil { get; private set; }

    public Guid? CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }

    private ReminderDispatch() { }

    public static ReminderDispatch Record(
        Guid reminderId,
        Guid userId,
        DateTimeOffset occurrenceStartsAt,
        Guid correlationId,
        bool isLate,
        TimeProvider timeProvider)
    {
        var now = timeProvider.GetUtcNow();
        return new ReminderDispatch
        {
            Id = Guid.CreateVersion7(),
            ReminderId = reminderId,
            UserId = userId,
            OccurrenceStartsAt = occurrenceStartsAt,
            DispatchedAt = now,
            CorrelationId = correlationId,
            IsLate = isLate,
            CreatedAt = now
        };
    }

    /// <summary>Acknowledges the occurrence. Idempotent: a second tap is a no-op. Clears any pending snooze.</summary>
    public void Acknowledge(TimeProvider timeProvider)
    {
        if (AcknowledgedAt is not null)
            return;

        AcknowledgedAt = timeProvider.GetUtcNow();
        SnoozedUntil = null;
    }

    /// <summary>Defers this occurrence; the sweep re-fires it once at <paramref name="until"/>. A no-op once acknowledged.</summary>
    public void Snooze(DateTimeOffset until)
    {
        if (AcknowledgedAt is not null)
            return;

        SnoozedUntil = until;
    }

    /// <summary>Whether this occurrence is waiting to be re-fired by a snooze that has now passed.</summary>
    public bool IsSnoozeDue(DateTimeOffset now) => AcknowledgedAt is null && SnoozedUntil is { } until && until <= now;

    /// <summary>Clears the snooze after re-firing, so one snooze yields exactly one re-fire.</summary>
    public void ClearSnooze() => SnoozedUntil = null;
}
