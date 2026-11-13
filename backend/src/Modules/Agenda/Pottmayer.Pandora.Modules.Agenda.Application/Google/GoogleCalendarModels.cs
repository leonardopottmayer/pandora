namespace Pottmayer.Pandora.Modules.Agenda.Application.Google;

// The Google Calendar API v3 resources Pandora reads, in their wire shape (camelCase JSON). Only the
// fields the sync uses are declared; the rest of each resource is ignored on deserialization.

/// <summary>An entry of <c>calendarList</c>: one calendar the account can see.</summary>
public sealed record GoogleCalendarListEntry(
    string Id,
    string? Summary,
    string? SummaryOverride,
    string? BackgroundColor,
    string? TimeZone,
    string? AccessRole,
    bool? Primary,
    IReadOnlyList<GoogleReminder>? DefaultReminders)
{
    /// <summary>What the user calls it: their own rename wins over the calendar's name.</summary>
    public string DisplayName => SummaryOverride ?? Summary ?? Id;
}

public sealed record GoogleReminder(string? Method, int Minutes);

/// <summary>A start or end: <c>date</c> (all-day, <c>yyyy-MM-dd</c>) or <c>dateTime</c> (with offset).</summary>
public sealed record GoogleEventTime(string? Date, DateTimeOffset? DateTime, string? TimeZone);

public sealed record GoogleEventReminders(bool UseDefault, IReadOnlyList<GoogleReminder>? Overrides);

public sealed record GoogleEntryPoint(string? EntryPointType, string? Uri);

public sealed record GoogleConferenceData(IReadOnlyList<GoogleEntryPoint>? EntryPoints);

/// <summary>
/// An event resource. A series carries <see cref="Recurrence"/>; one changed or cancelled occurrence of
/// a series is its own resource, with <see cref="RecurringEventId"/> and <see cref="OriginalStartTime"/>.
/// </summary>
public sealed record GoogleEvent(
    string Id,
    string? Status,
    string? Etag,
    DateTimeOffset? Updated,
    string? Summary,
    string? Description,
    string? Location,
    string? HangoutLink,
    GoogleConferenceData? ConferenceData,
    GoogleEventTime? Start,
    GoogleEventTime? End,
    IReadOnlyList<string>? Recurrence,
    string? RecurringEventId,
    GoogleEventTime? OriginalStartTime,
    GoogleEventReminders? Reminders)
{
    public bool IsCancelled => string.Equals(Status, "cancelled", StringComparison.OrdinalIgnoreCase);
}

/// <summary>Every page of one <c>events.list</c>, and the cursor Google gave on its last page.</summary>
public sealed record GoogleEventListing(IReadOnlyList<GoogleEvent> Events, string? NextSyncToken);

/// <summary>
/// What Pandora sends on an insert or a patch. A null field is left out of the JSON, so a patch keeps
/// what Pandora does not hold (attendees, conference, colors); an empty string or list clears a field.
/// </summary>
public sealed record GoogleEventWrite(
    string? Id,
    string? Status,
    string? Summary,
    string? Description,
    string? Location,
    GoogleEventTime? Start,
    GoogleEventTime? End,
    IReadOnlyList<string>? Recurrence,
    GoogleEventReminders? Reminders);
