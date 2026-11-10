using Pottmayer.Tars.Core.Primitives.Outcomes;

namespace Pottmayer.Pandora.Modules.Agenda.Application.Google;

/// <summary>
/// Google Calendar API v3, read side, with the access token of an Integrations account. Implemented in
/// Infrastructure over HTTP; it fails with <see cref="Errors.GoogleCalendarErrors"/>, never throws.
/// </summary>
public interface IGoogleCalendarClient
{
    Task<Result<IReadOnlyList<GoogleCalendarListEntry>>> ListCalendarsAsync(
        Guid userId, Guid accountId, CancellationToken ct = default);

    Task<Result<GoogleCalendarListEntry>> GetCalendarAsync(
        Guid userId, Guid accountId, string calendarId, CancellationToken ct = default);

    /// <summary>
    /// Every page of <c>events.list</c>. With a <paramref name="syncToken"/>, only what changed since it
    /// (cancellations included); without, every event from <paramref name="timeMin"/> on. An expired
    /// token fails with <see cref="Errors.GoogleCalendarErrors.SyncTokenExpired"/>.
    /// </summary>
    Task<Result<GoogleEventListing>> ListEventsAsync(
        Guid userId, Guid accountId, string calendarId, string? syncToken, DateTimeOffset? timeMin,
        CancellationToken ct = default);

    /// <summary>The occurrences of a series between two instants, as Google expands it.</summary>
    Task<Result<IReadOnlyList<GoogleEvent>>> ListInstancesAsync(
        Guid userId, Guid accountId, string calendarId, string eventId, DateTimeOffset timeMin, DateTimeOffset timeMax,
        CancellationToken ct = default);
}
