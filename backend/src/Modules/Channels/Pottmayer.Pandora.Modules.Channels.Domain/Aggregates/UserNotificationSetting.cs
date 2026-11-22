using Pottmayer.Pandora.Modules.Channels.Domain.ValueObjects;
using Pottmayer.Pandora.Shared.Domain;
using Pottmayer.Tars.Core.Ddd;

namespace Pottmayer.Pandora.Modules.Channels.Domain.Aggregates;

/// <summary>
/// A user's cross-category delivery settings. Today that is one thing: quiet hours — a daily window,
/// expressed in the user's own time zone, during which notifications are suppressed. It is global on
/// purpose (one "do not disturb", not one per category); per-category muting already lives on
/// <see cref="NotificationPreference"/>. Security notifications never reach this — they take the
/// fact→template path and are mandatory.
/// </summary>
/// <remarks>
/// The window is stored as two wall-clock times with no date. It is the caller's job to hand
/// <see cref="ShouldSuppress"/> the user's <em>local</em> time-of-day; this aggregate does no
/// zone math, so it never needs to know which zone the user is in.
/// </remarks>
public sealed class UserNotificationSetting : AggregateRoot<Guid>, IAuditable
{
    public Guid UserId { get; private set; }

    /// <summary>Start of the daily quiet window (local wall clock). Null when quiet hours are off.</summary>
    public TimeOnly? QuietHoursStart { get; private set; }

    /// <summary>End of the daily quiet window (local wall clock), exclusive. Null when quiet hours are off.</summary>
    public TimeOnly? QuietHoursEnd { get; private set; }

    /// <summary>What to do with a delivery that lands in the window. Null when quiet hours are off.</summary>
    public QuietHoursBehaviour? QuietHoursBehaviour { get; private set; }

    public Guid? CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }

    private UserNotificationSetting() { }

    public static UserNotificationSetting Create(Guid userId, TimeProvider timeProvider) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            CreatedAt = timeProvider.GetUtcNow()
        };

    /// <summary>Turns quiet hours on for a window. A zero-length window (start == end) is rejected upstream.</summary>
    public void SetQuietHours(TimeOnly start, TimeOnly end, QuietHoursBehaviour behaviour)
    {
        QuietHoursStart = start;
        QuietHoursEnd = end;
        QuietHoursBehaviour = behaviour;
    }

    /// <summary>Turns quiet hours off. Deliveries are never suppressed on this account again until re-set.</summary>
    public void ClearQuietHours()
    {
        QuietHoursStart = null;
        QuietHoursEnd = null;
        QuietHoursBehaviour = null;
    }

    /// <summary>True when quiet hours are configured with a window.</summary>
    public bool QuietHoursEnabled =>
        QuietHoursStart is not null && QuietHoursEnd is not null && QuietHoursBehaviour is not null;

    /// <summary>
    /// Whether the given local time-of-day falls inside the quiet window. Handles a window that wraps
    /// past midnight (e.g. 22:00–07:00). The start is inclusive, the end exclusive.
    /// </summary>
    public bool IsWithinQuietHours(TimeOnly localTime)
    {
        if (QuietHoursStart is not { } start || QuietHoursEnd is not { } end)
            return false;

        // Same-day window: [start, end).
        if (start < end)
            return localTime >= start && localTime < end;

        // Overnight window: [start, midnight) ∪ [midnight, end).
        return localTime >= start || localTime < end;
    }

    /// <summary>
    /// Whether a notification at the given local time-of-day should be dropped: quiet hours are on,
    /// the time is inside the window, and the behaviour is <see cref="ValueObjects.QuietHoursBehaviour.Suppress"/>.
    /// </summary>
    public bool ShouldSuppress(TimeOnly localTime) =>
        QuietHoursBehaviour == ValueObjects.QuietHoursBehaviour.Suppress && IsWithinQuietHours(localTime);
}
