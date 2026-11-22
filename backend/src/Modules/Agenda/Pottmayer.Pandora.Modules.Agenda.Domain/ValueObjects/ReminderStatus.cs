namespace Pottmayer.Pandora.Modules.Agenda.Domain.ValueObjects;

/// <summary>Lifecycle of a reminder. It fires once, then is acknowledged, snoozed or cancelled.</summary>
public enum ReminderStatus
{
    /// <summary>Waiting to fire at its remind time.</summary>
    Scheduled,

    /// <summary>The alert was dispatched; awaiting the user's acknowledge or snooze.</summary>
    Notified,

    /// <summary>The user acknowledged it. Terminal.</summary>
    Acknowledged,

    /// <summary>Deferred to <c>SnoozedUntil</c>; the sweep will fire it again then.</summary>
    Snoozed,

    /// <summary>Cancelled before it fired. Terminal.</summary>
    Cancelled,
}
