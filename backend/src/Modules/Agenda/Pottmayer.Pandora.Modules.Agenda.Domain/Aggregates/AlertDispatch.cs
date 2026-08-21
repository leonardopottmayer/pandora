using Pottmayer.Pandora.Shared.Domain;
using Pottmayer.Tars.Core.Ddd;

namespace Pottmayer.Pandora.Modules.Agenda.Domain.Aggregates;

/// <summary>
/// The record that an alert was dispatched for one subject anchor (doc agd008). Its unique
/// <c>(alert_id, occurrence_starts_at)</c> key is the sweep's idempotency guard: re-running a tick — or
/// restarting mid-tick — never double-fires an anchor.
///
/// <para>For a task, the anchor is the task's <c>DueAt</c>. Unlike the reminder ledger, this one carries
/// no acknowledge/snooze: a task alert's button completes the task itself (acting on the task row), and
/// task alerts have no per-occurrence snooze.</para>
/// </summary>
public sealed class AlertDispatch : AggregateRoot<Guid>, IAuditable
{
    public Guid AlertId { get; private set; }
    public Guid UserId { get; private set; }

    /// <summary>The subject anchor (UTC) this dispatch fired for. Unique with <see cref="AlertId"/>.</summary>
    public DateTimeOffset OccurrenceStartsAt { get; private set; }

    public DateTimeOffset DispatchedAt { get; private set; }

    /// <summary>The correlation id handed to Channels, so a delivery is traceable end to end.</summary>
    public Guid CorrelationId { get; private set; }

    /// <summary>Fired from the grace window rather than on its own tick. Informational.</summary>
    public bool IsLate { get; private set; }

    public Guid? CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }

    private AlertDispatch() { }

    public static AlertDispatch Record(
        Guid alertId,
        Guid userId,
        DateTimeOffset occurrenceStartsAt,
        Guid correlationId,
        bool isLate,
        TimeProvider timeProvider)
    {
        var now = timeProvider.GetUtcNow();
        return new AlertDispatch
        {
            Id = Guid.CreateVersion7(),
            AlertId = alertId,
            UserId = userId,
            OccurrenceStartsAt = occurrenceStartsAt,
            DispatchedAt = now,
            CorrelationId = correlationId,
            IsLate = isLate,
            CreatedAt = now
        };
    }
}
