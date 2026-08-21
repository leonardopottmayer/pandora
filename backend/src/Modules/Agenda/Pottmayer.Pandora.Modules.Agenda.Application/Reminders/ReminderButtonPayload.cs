using System.Globalization;

namespace Pottmayer.Pandora.Modules.Agenda.Application.Reminders;

/// <summary>
/// Encodes what an inline button carries back for a reminder. A single-shot button carries just the
/// reminder id (Phase 1, unchanged). A recurring button also carries the occurrence it fired for, so
/// acknowledge/snooze act on that occurrence, not the series. The value is opaque to Channels, which
/// stores and returns it verbatim — so enriching it here touches nothing downstream.
/// </summary>
public static class ReminderButtonPayload
{
    private const char Separator = '|';

    /// <summary>The payload for a single-shot reminder's button.</summary>
    public static string ForReminder(Guid reminderId) => reminderId.ToString();

    /// <summary>
    /// The payload for a recurring reminder occurrence's button. The occurrence is encoded as UTC
    /// ticks so it round-trips to the exact anchor the dispatch ledger is keyed on (microsecond DB
    /// precision and finer), rather than being truncated to whole milliseconds.
    /// </summary>
    public static string ForOccurrence(Guid reminderId, DateTimeOffset occurrenceStartsAt) =>
        $"{reminderId}{Separator}{occurrenceStartsAt.UtcTicks}";

    /// <summary>
    /// Parses a button payload. <paramref name="occurrenceStartsAt"/> is non-null for a recurring
    /// occurrence, null for a single-shot reminder. Returns false if the payload is unparseable.
    /// </summary>
    public static bool TryParse(string? payload, out Guid reminderId, out DateTimeOffset? occurrenceStartsAt)
    {
        reminderId = Guid.Empty;
        occurrenceStartsAt = null;

        if (string.IsNullOrWhiteSpace(payload))
            return false;

        var separatorIndex = payload.IndexOf(Separator);
        if (separatorIndex < 0)
            return Guid.TryParse(payload, out reminderId);

        if (!Guid.TryParse(payload[..separatorIndex], out reminderId))
            return false;

        if (!long.TryParse(payload[(separatorIndex + 1)..], NumberStyles.Integer, CultureInfo.InvariantCulture, out var utcTicks)
            || utcTicks < DateTimeOffset.MinValue.UtcTicks || utcTicks > DateTimeOffset.MaxValue.UtcTicks)
            return false;

        occurrenceStartsAt = new DateTimeOffset(utcTicks, TimeSpan.Zero);
        return true;
    }
}
