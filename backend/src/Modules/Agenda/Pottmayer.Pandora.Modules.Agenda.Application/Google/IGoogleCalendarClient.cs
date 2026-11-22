using Pottmayer.Tars.Core.Primitives.Outcomes;

namespace Pottmayer.Pandora.Modules.Agenda.Application.Google;

/// <summary>
/// Google Calendar API v3 with the access token of an Integrations account. Implemented in
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

    /// <summary>One event or instance. Missing ⇒ <see cref="Errors.GoogleCalendarErrors.EventNotFound"/>.</summary>
    Task<Result<GoogleEvent>> GetEventAsync(
        Guid userId, Guid accountId, string calendarId, string eventId, CancellationToken ct = default);

    /// <summary>Inserts with the id in <paramref name="body"/>; an id Google has ⇒ <see cref="Errors.GoogleCalendarErrors.EventExists"/>.</summary>
    Task<Result<GoogleEvent>> InsertEventAsync(
        Guid userId, Guid accountId, string calendarId, GoogleEventWrite body, CancellationToken ct = default);

    /// <summary>
    /// Patches an event or one instance of a series. With <paramref name="ifMatch"/>, a change in Google
    /// since that etag fails with <see cref="Errors.GoogleCalendarErrors.EventChanged"/>.
    /// </summary>
    Task<Result<GoogleEvent>> PatchEventAsync(
        Guid userId, Guid accountId, string calendarId, string eventId, GoogleEventWrite body, string? ifMatch,
        CancellationToken ct = default);

    /// <summary>Deletes an event, or cancels one instance. Already gone counts as done.</summary>
    Task<Result<bool>> DeleteEventAsync(
        Guid userId, Guid accountId, string calendarId, string eventId, CancellationToken ct = default);
}
