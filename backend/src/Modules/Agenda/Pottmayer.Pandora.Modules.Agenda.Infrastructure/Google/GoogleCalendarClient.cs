using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Pottmayer.Pandora.Modules.Agenda.Application.Errors;
using Pottmayer.Pandora.Modules.Agenda.Application.Google;
using Pottmayer.Pandora.Modules.Integrations.Abstractions.Ports;
using Pottmayer.Tars.Core.Primitives.Outcomes;

namespace Pottmayer.Pandora.Modules.Agenda.Infrastructure.Google;

/// <summary>
/// Google Calendar API v3 over REST (no Google SDK, like the Gmail adapter), with the access token of
/// the binding's Integrations account. Owns the wire only; the mapping to events is
/// <see cref="GoogleEventMapper"/>.
/// </summary>
internal sealed class GoogleCalendarClient(HttpClient http, IExternalCredentialProvider credentials) : IGoogleCalendarClient
{
    internal const string BaseUrl = "https://www.googleapis.com/calendar/v3/";

    /// <summary>Any calendar scope will do for reading (<c>calendar</c>, <c>calendar.readonly</c>, <c>calendar.events</c>…).</summary>
    internal const string ScopePrefix = "https://www.googleapis.com/auth/calendar";

    private const int PageSize = 250;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        // A patch must leave out what Pandora does not hold, not send it as null.
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private static readonly string[] RateLimitReasons = ["rateLimitExceeded", "userRateLimitExceeded"];

    /// <summary>Pause before each retry of a throttled request; the server's <c>Retry-After</c> wins when sent.</summary>
    internal IReadOnlyList<TimeSpan> RetryDelays { get; init; } =
        [TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(4), TimeSpan.FromSeconds(8)];

    public Task<Result<IReadOnlyList<GoogleCalendarListEntry>>> ListCalendarsAsync(
        Guid userId, Guid accountId, CancellationToken ct = default) =>
        RunAsync<IReadOnlyList<GoogleCalendarListEntry>>(userId, accountId, async (token, c) =>
        {
            var all = new List<GoogleCalendarListEntry>();
            string? pageToken = null;
            do
            {
                var url = $"users/me/calendarList?maxResults={PageSize}" + PageParam(pageToken);
                var page = await GetAsync<Page<GoogleCalendarListEntry>>(token, url, c);
                all.AddRange(page.Items ?? []);
                pageToken = page.NextPageToken;
            } while (pageToken is not null);
            return all;
        }, ct);

    public Task<Result<GoogleCalendarListEntry>> GetCalendarAsync(
        Guid userId, Guid accountId, string calendarId, CancellationToken ct = default) =>
        RunAsync(userId, accountId, (token, c) =>
            GetAsync<GoogleCalendarListEntry>(token, $"users/me/calendarList/{Uri.EscapeDataString(calendarId)}", c,
                notFound: GoogleCalendarErrors.CalendarNotFound), ct);

    public Task<Result<GoogleEventListing>> ListEventsAsync(
        Guid userId, Guid accountId, string calendarId, string? syncToken, DateTimeOffset? timeMin,
        CancellationToken ct = default) =>
        RunAsync(userId, accountId, async (token, c) =>
        {
            var events = new List<GoogleEvent>();
            string? pageToken = null;
            string? nextSyncToken = null;
            do
            {
                // singleEvents=false: a series comes once, with its exceptions as their own resources.
                var url = $"calendars/{Uri.EscapeDataString(calendarId)}/events?maxResults={PageSize}&singleEvents=false";
                if (syncToken is not null)
                    url += "&syncToken=" + Uri.EscapeDataString(syncToken);
                else if (timeMin is { } min)
                    url += "&timeMin=" + Uri.EscapeDataString(min.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'"));
                url += PageParam(pageToken);

                var page = await GetAsync<EventPage>(token, url, c, notFound: GoogleCalendarErrors.CalendarNotFound);
                events.AddRange(page.Items ?? []);
                pageToken = page.NextPageToken;
                nextSyncToken = page.NextSyncToken ?? nextSyncToken;
            } while (pageToken is not null);

            return new GoogleEventListing(events, nextSyncToken);
        }, ct);

    public Task<Result<IReadOnlyList<GoogleEvent>>> ListInstancesAsync(
        Guid userId, Guid accountId, string calendarId, string eventId, DateTimeOffset timeMin, DateTimeOffset timeMax,
        CancellationToken ct = default) =>
        RunAsync<IReadOnlyList<GoogleEvent>>(userId, accountId, async (token, c) =>
        {
            var all = new List<GoogleEvent>();
            string? pageToken = null;
            do
            {
                var url = $"calendars/{Uri.EscapeDataString(calendarId)}/events/{Uri.EscapeDataString(eventId)}/instances"
                          + $"?maxResults={PageSize}&showDeleted=true"
                          + "&timeMin=" + Uri.EscapeDataString(timeMin.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'"))
                          + "&timeMax=" + Uri.EscapeDataString(timeMax.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'"))
                          + PageParam(pageToken);
                var page = await GetAsync<Page<GoogleEvent>>(token, url, c, notFound: GoogleCalendarErrors.CalendarNotFound);
                all.AddRange(page.Items ?? []);
                pageToken = page.NextPageToken;
            } while (pageToken is not null);
            return all;
        }, ct);

    public Task<Result<GoogleEvent>> GetEventAsync(
        Guid userId, Guid accountId, string calendarId, string eventId, CancellationToken ct = default) =>
        RunAsync(userId, accountId, (token, c) =>
            SendAsync<GoogleEvent>(token, HttpMethod.Get, EventPath(calendarId, eventId), null, null, c, EventStatus), ct);

    public Task<Result<GoogleEvent>> InsertEventAsync(
        Guid userId, Guid accountId, string calendarId, GoogleEventWrite body, CancellationToken ct = default) =>
        RunAsync(userId, accountId, (token, c) =>
            SendAsync<GoogleEvent>(token, HttpMethod.Post, $"calendars/{Uri.EscapeDataString(calendarId)}/events", body, null, c,
                status => status == HttpStatusCode.Conflict ? GoogleCalendarErrors.EventExists : EventStatus(status)), ct);

    public Task<Result<GoogleEvent>> PatchEventAsync(
        Guid userId, Guid accountId, string calendarId, string eventId, GoogleEventWrite body, string? ifMatch,
        CancellationToken ct = default) =>
        RunAsync(userId, accountId, (token, c) =>
            SendAsync<GoogleEvent>(token, HttpMethod.Patch, EventPath(calendarId, eventId), body, ifMatch, c, EventStatus), ct);

    public Task<Result<bool>> DeleteEventAsync(
        Guid userId, Guid accountId, string calendarId, string eventId, CancellationToken ct = default) =>
        RunAsync(userId, accountId, async (token, c) =>
        {
            try
            {
                await SendAsync<JsonElement?>(token, HttpMethod.Delete, EventPath(calendarId, eventId), null, null, c, EventStatus);
            }
            catch (GoogleCalendarException ex) when (ex.Error.Code == GoogleCalendarErrors.EventNotFound.Code)
            {
                // Already gone: what the delete wanted.
            }
            return true;
        }, ct);

    private static string EventPath(string calendarId, string eventId) =>
        $"calendars/{Uri.EscapeDataString(calendarId)}/events/{Uri.EscapeDataString(eventId)}";

    /// <summary>The answers specific to one event: missing or deleted, and changed since the etag sent.</summary>
    private static Error? EventStatus(HttpStatusCode status) => status switch
    {
        HttpStatusCode.NotFound or HttpStatusCode.Gone => GoogleCalendarErrors.EventNotFound,
        HttpStatusCode.PreconditionFailed => GoogleCalendarErrors.EventChanged,
        _ => null
    };

    private static string PageParam(string? pageToken) =>
        pageToken is null ? string.Empty : "&pageToken=" + Uri.EscapeDataString(pageToken);

    private async Task<Result<T>> RunAsync<T>(Guid userId, Guid accountId, Func<string, CancellationToken, Task<T>> work, CancellationToken ct)
    {
        var token = await credentials.GetAccessTokenAsync(userId, accountId, ct);
        if (!token.IsSuccess)
            return Result<T>.Failure(GoogleCalendarErrors.AuthFailed(string.Join("; ", token.Errors.Select(e => e.Message))));

        if (!token.Value!.Scopes.Any(s => s.StartsWith(ScopePrefix, StringComparison.Ordinal)))
            return Result<T>.Failure(GoogleCalendarErrors.MissingScope);

        try
        {
            return Result<T>.Success(await work(token.Value.Token, ct));
        }
        catch (GoogleCalendarException ex)
        {
            return Result<T>.Failure(ex.Error);
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            return Result<T>.Failure(GoogleCalendarErrors.Failed(ex.Message));
        }
    }

    private Task<T> GetAsync<T>(string token, string path, CancellationToken ct, Error? notFound = null) =>
        SendAsync<T>(token, HttpMethod.Get, path, null, null, ct, status => status == HttpStatusCode.NotFound ? notFound : null);

    private async Task<T> SendAsync<T>(
        string token, HttpMethod method, string path, object? body, string? ifMatch, CancellationToken ct,
        Func<HttpStatusCode, Error?> specific)
    {
        for (var attempt = 0; ; attempt++)
        {
            using var request = new HttpRequestMessage(method, BaseUrl + path);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            if (body is not null)
                request.Content = JsonContent.Create(body, body.GetType(), options: Json);
            if (ifMatch is not null)
                request.Headers.TryAddWithoutValidation("If-Match", ifMatch); // Google's etags come already quoted
            using var response = await http.SendAsync(request, ct);

            if (response.StatusCode == HttpStatusCode.NoContent)
                return default!;
            if (response.IsSuccessStatusCode)
                return (await response.Content.ReadFromJsonAsync<T>(Json, ct))!;

            var text = await response.Content.ReadAsStringAsync(ct);
            var throttled = IsRateLimited(response.StatusCode, text) || (int)response.StatusCode >= 500;
            if (throttled && attempt < RetryDelays.Count)
            {
                await Task.Delay(response.Headers.RetryAfter?.Delta ?? RetryDelays[attempt], ct);
                continue;
            }

            throw new GoogleCalendarException(specific(response.StatusCode) ?? ToError(response.StatusCode, text, path));
        }
    }

    private static bool IsRateLimited(HttpStatusCode status, string body) =>
        status == HttpStatusCode.TooManyRequests
        || (status == HttpStatusCode.Forbidden && RateLimitReasons.Any(r => body.Contains(r, StringComparison.Ordinal)));

    private static Error ToError(HttpStatusCode status, string body, string path) => status switch
    {
        HttpStatusCode.Gone => GoogleCalendarErrors.SyncTokenExpired,
        HttpStatusCode.Unauthorized => GoogleCalendarErrors.AuthFailed("Google rejected the access token."),
        HttpStatusCode.Forbidden when body.Contains("insufficientPermissions", StringComparison.Ordinal)
            || body.Contains("ACCESS_TOKEN_SCOPE_INSUFFICIENT", StringComparison.Ordinal) => GoogleCalendarErrors.MissingScope,
        _ => GoogleCalendarErrors.Failed($"Google Calendar answered {(int)status} on {path.Split('?')[0]}.")
    };

    private sealed record Page<T>(IReadOnlyList<T>? Items, string? NextPageToken);

    private sealed record EventPage(IReadOnlyList<GoogleEvent>? Items, string? NextPageToken, string? NextSyncToken);

    private sealed class GoogleCalendarException(Error error) : Exception(error.Message)
    {
        public Error Error { get; } = error;
    }
}
