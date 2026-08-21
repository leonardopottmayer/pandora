using Pottmayer.Pandora.Modules.Agenda.Domain.ValueObjects;
using Pottmayer.Pandora.Shared.Domain;
using Pottmayer.Tars.Core.Ddd;

namespace Pottmayer.Pandora.Modules.Agenda.Domain.Aggregates;

/// <summary>
/// A ping at an instant. No workflow: it fires, and then it is acknowledged, snoozed or cancelled.
/// The status is also the dispatch guard — once <see cref="ReminderStatus.Notified"/>, the sweep will
/// not fire it again, which is what makes the sweep idempotent across restarts without a ledger.
/// </summary>
public sealed class Reminder : AggregateRoot<Guid>, IAuditable
{
    private const int MaxTitleLength = 200;

    public Guid UserId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string? Notes { get; private set; }

    /// <summary>The instant it fires. Absolute (UTC); displayed and recurred in <see cref="TimeZone"/>.</summary>
    public DateTimeOffset RemindAt { get; private set; }

    /// <summary>IANA time zone the reminder is shown in. Defaults to UTC until Identity carries a user default.</summary>
    public string TimeZone { get; private set; } = "UTC";

    public ReminderStatus Status { get; private set; }

    /// <summary>Set by a snooze; the sweep treats it as the effective remind time.</summary>
    public DateTimeOffset? SnoozedUntil { get; private set; }

    public DateTimeOffset? AcknowledgedAt { get; private set; }

    public Guid? CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }

    private Reminder() { }

    public static Reminder Create(
        Guid userId, string title, string? notes, DateTimeOffset remindAt, string timeZone, TimeProvider timeProvider)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("A reminder needs a title.", nameof(title));

        return new Reminder
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            Title = title.Length <= MaxTitleLength ? title.Trim() : title[..MaxTitleLength],
            Notes = notes,
            RemindAt = remindAt,
            TimeZone = string.IsNullOrWhiteSpace(timeZone) ? "UTC" : timeZone,
            Status = ReminderStatus.Scheduled,
            CreatedAt = timeProvider.GetUtcNow()
        };
    }

    /// <summary>When the reminder should fire: the snooze time if snoozed, else the remind time.</summary>
    public DateTimeOffset EffectiveRemindAt => SnoozedUntil ?? RemindAt;

    /// <summary>Whether the sweep should fire it now.</summary>
    public bool IsDue(DateTimeOffset now) =>
        Status is ReminderStatus.Scheduled or ReminderStatus.Snoozed && EffectiveRemindAt <= now;

    /// <summary>Records that the alert was dispatched. Clears the snooze it was firing for.</summary>
    public void MarkNotified()
    {
        Status = ReminderStatus.Notified;
        SnoozedUntil = null;
    }

    /// <summary>Defers the reminder; it fires again at <paramref name="until"/>. A no-op once terminal.</summary>
    public void Snooze(DateTimeOffset until)
    {
        if (Status is ReminderStatus.Acknowledged or ReminderStatus.Cancelled)
            return;

        SnoozedUntil = until;
        Status = ReminderStatus.Snoozed;
    }

    /// <summary>Acknowledges the reminder. A no-op once terminal, so a double tap is harmless.</summary>
    public void Acknowledge(TimeProvider timeProvider)
    {
        if (Status is ReminderStatus.Acknowledged or ReminderStatus.Cancelled)
            return;

        Status = ReminderStatus.Acknowledged;
        AcknowledgedAt = timeProvider.GetUtcNow();
        SnoozedUntil = null;
    }

    /// <summary>Cancels the reminder before it is acted on.</summary>
    public void Cancel()
    {
        if (Status is ReminderStatus.Acknowledged or ReminderStatus.Cancelled)
            return;

        Status = ReminderStatus.Cancelled;
        SnoozedUntil = null;
    }
}
