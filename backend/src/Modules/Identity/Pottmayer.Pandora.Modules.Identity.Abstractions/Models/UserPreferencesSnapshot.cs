namespace Pottmayer.Pandora.Modules.Identity.Abstractions.Models;

/// <summary>
/// A read-only view of the scheduling-relevant slice of a user's preferences, for consumers (e.g.
/// Agenda) that need the reference clock and calendar defaults without touching Identity's schema.
/// </summary>
public sealed record UserPreferencesSnapshot(
    /// <summary>IANA time zone (e.g. "America/Sao_Paulo"). The reference clock for the Agenda.</summary>
    string TimeZone,
    /// <summary>First day of the week, for calendar rendering.</summary>
    DayOfWeek WeekStartsOn,
    /// <summary>Default signed offset, in minutes, for alerts on events and tasks (e.g. -15 = fifteen minutes before).</summary>
    int DefaultAlertOffsetMinutes);
