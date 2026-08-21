using Pottmayer.Pandora.Modules.Agenda.Domain.Recurrence;
using Pottmayer.Pandora.Modules.Agenda.Domain.ValueObjects;
using Pottmayer.Pandora.Shared.Domain;
using Pottmayer.Tars.Core.Ddd;

namespace Pottmayer.Pandora.Modules.Agenda.Domain.Aggregates;

/// <summary>
/// A ping at an instant.
///
/// <para><b>Single-shot</b> (<see cref="Rrule"/> null): no workflow — it fires, and then it is
/// acknowledged, snoozed or cancelled. Its status is the dispatch guard: once
/// <see cref="ReminderStatus.Notified"/>, the sweep will not fire it again, which is what makes the
/// single-shot sweep idempotent across restarts without a ledger.</para>
///
/// <para><b>Recurring</b> (<see cref="Rrule"/> set): fires once per occurrence expanded from the rule,
/// anchored at <see cref="RemindAt"/> in <see cref="TimeZone"/>. The status is <em>not</em> the guard
/// here — a recurring reminder stays <see cref="ReminderStatus.Scheduled"/> for the life of the series
/// and idempotency is the per-occurrence dispatch ledger. Acknowledge and snooze act on an occurrence,
/// not the series; cancelling stops the whole series.</para>
/// </summary>
public sealed class Reminder : AggregateRoot<Guid>, IAuditable
{
    private const int MaxTitleLength = 200;

    public Guid UserId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string? Notes { get; private set; }

    /// <summary>
    /// The instant it fires (single-shot) or the series anchor / DTSTART (recurring). Absolute (UTC);
    /// displayed and recurred in <see cref="TimeZone"/>.
    /// </summary>
    public DateTimeOffset RemindAt { get; private set; }

    /// <summary>IANA time zone the reminder is shown in. Defaults to UTC until Identity carries a user default.</summary>
    public string TimeZone { get; private set; } = "UTC";

    /// <summary>Raw RRULE (RFC 5545 subset), stored verbatim. Null ⇒ single-shot.</summary>
    public string? Rrule { get; private set; }

    /// <summary>Denormalized last-occurrence bound (from UNTIL/COUNT), so the sweep prunes by index. Null ⇒ open-ended.</summary>
    public DateTimeOffset? RecurrenceEndsAt { get; private set; }

    public ReminderStatus Status { get; private set; }

    /// <summary>Set by a snooze; the sweep treats it as the effective remind time.</summary>
    public DateTimeOffset? SnoozedUntil { get; private set; }

    public DateTimeOffset? AcknowledgedAt { get; private set; }

    public Guid? CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }

    private Reminder() { }

    /// <summary>Whether this reminder repeats. Recurring reminders are driven by the dispatch ledger, not status.</summary>
    public bool IsRecurring => Rrule is not null;

    public static Reminder Create(
        Guid userId,
        string title,
        string? notes,
        DateTimeOffset remindAt,
        string timeZone,
        TimeProvider timeProvider,
        string? rrule = null)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("A reminder needs a title.", nameof(title));

        var zone = string.IsNullOrWhiteSpace(timeZone) ? "UTC" : timeZone;

        string? storedRrule = null;
        DateTimeOffset? recurrenceEndsAt = null;
        if (!string.IsNullOrWhiteSpace(rrule))
        {
            // Parse rejects anything outside the supported subset — this is the write-time guard.
            var rule = RecurrenceRule.Parse(rrule);
            storedRrule = rule.Raw;
            recurrenceEndsAt = rule.ComputeEndsAt(remindAt, ResolveZone(zone));
        }

        return new Reminder
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            Title = title.Length <= MaxTitleLength ? title.Trim() : title[..MaxTitleLength],
            Notes = notes,
            RemindAt = remindAt,
            TimeZone = zone,
            Rrule = storedRrule,
            RecurrenceEndsAt = recurrenceEndsAt,
            Status = ReminderStatus.Scheduled,
            CreatedAt = timeProvider.GetUtcNow()
        };
    }

    /// <summary>The reminder's zone as a <see cref="TimeZoneInfo"/>, throwing on an unknown IANA id.</summary>
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
