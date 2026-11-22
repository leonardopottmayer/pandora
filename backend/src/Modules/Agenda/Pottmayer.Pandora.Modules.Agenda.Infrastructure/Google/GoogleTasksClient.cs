using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Pottmayer.Pandora.Modules.Agenda.Application.Errors;
using Pottmayer.Pandora.Modules.Agenda.Application.Google;
using Pottmayer.Pandora.Modules.Integrations.Abstractions.Ports;
using Pottmayer.Tars.Core.Primitives.Outcomes;

namespace Pottmayer.Pandora.Modules.Agenda.Infrastructure.Google;

/// <summary>
/// Google Tasks API v1 over REST, with the access token of the binding's Integrations account. Owns the
/// wire only; the mapping to tasks is <see cref="GoogleTaskMapper"/>.
/// </summary>
/// <remarks>ponytail: the auth, retry and error plumbing repeats <see cref="GoogleCalendarClient"/>'s; pull a shared base out if a third Google API comes.</remarks>
internal sealed class GoogleTasksClient(HttpClient http, IExternalCredentialProvider credentials) : IGoogleTasksClient
{
    internal const string BaseUrl = "https://tasks.googleapis.com/tasks/v1/";
    internal const string Scope = "https://www.googleapis.com/auth/tasks";

    private const int PageSize = 100;

    // Nulls are written: an update replaces the task, and a null due date is how it is cleared.
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private static readonly string[] RateLimitReasons = ["rateLimitExceeded", "userRateLimitExceeded"];

    /// <summary>Pause before each retry of a throttled request; the server's <c>Retry-After</c> wins when sent.</summary>
    internal IReadOnlyList<TimeSpan> RetryDelays { get; init; } =
        [TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(4), TimeSpan.FromSeconds(8)];

    public Task<Result<IReadOnlyList<GoogleTaskListEntry>>> ListTaskListsAsync(Guid userId, Guid accountId, CancellationToken ct = default) =>
        RunAsync<IReadOnlyList<GoogleTaskListEntry>>(userId, accountId, async (token, c) =>
        {
            var all = new List<GoogleTaskListEntry>();
            string? pageToken = null;
            do
            {
                var page = await SendAsync<Page<GoogleTaskListEntry>>(
                    token, HttpMethod.Get, $"users/@me/lists?maxResults={PageSize}" + PageParam(pageToken), null, c, _ => null);
                all.AddRange(page.Items ?? []);
                pageToken = page.NextPageToken;
            } while (pageToken is not null);
            return all;
        }, ct);

    public Task<Result<IReadOnlyList<GoogleTask>>> ListTasksAsync(
        Guid userId, Guid accountId, string listId, DateTimeOffset? updatedMin, CancellationToken ct = default) =>
        RunAsync<IReadOnlyList<GoogleTask>>(userId, accountId, async (token, c) =>
        {
            var all = new List<GoogleTask>();
            string? pageToken = null;
            do
            {
                // Completed tasks cleared in Google's UI are hidden: they are still tasks Pandora holds.
                var url = $"lists/{Uri.EscapeDataString(listId)}/tasks?maxResults={PageSize}&showCompleted=true&showHidden=true";
                if (updatedMin is { } min)
                    url += "&showDeleted=true&updatedMin=" + Uri.EscapeDataString(min.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'"));
                var page = await SendAsync<Page<GoogleTask>>(token, HttpMethod.Get, url + PageParam(pageToken), null, c,
                    status => status == HttpStatusCode.NotFound ? GoogleTasksErrors.ListNotFound : null);
                all.AddRange(page.Items ?? []);
                pageToken = page.NextPageToken;
            } while (pageToken is not null);
            return all;
        }, ct);

    public Task<Result<GoogleTask>> GetTaskAsync(Guid userId, Guid accountId, string listId, string taskId, CancellationToken ct = default) =>
        RunAsync(userId, accountId, (token, c) =>
            SendAsync<GoogleTask>(token, HttpMethod.Get, TaskPath(listId, taskId), null, c, TaskAnswer), ct);

    public Task<Result<GoogleTask>> InsertTaskAsync(
        Guid userId, Guid accountId, string listId, string? parentId, GoogleTaskWrite body, CancellationToken ct = default) =>
        RunAsync(userId, accountId, (token, c) =>
            SendAsync<GoogleTask>(token, HttpMethod.Post,
                $"lists/{Uri.EscapeDataString(listId)}/tasks" + (parentId is null ? "" : "?parent=" + Uri.EscapeDataString(parentId)),
                body, c, status => status == HttpStatusCode.NotFound ? GoogleTasksErrors.ListNotFound : null), ct);

    public Task<Result<GoogleTask>> UpdateTaskAsync(
        Guid userId, Guid accountId, string listId, string taskId, GoogleTaskWrite body, CancellationToken ct = default) =>
        RunAsync(userId, accountId, (token, c) =>
            SendAsync<GoogleTask>(token, HttpMethod.Put, TaskPath(listId, taskId), body, c, TaskAnswer), ct);

    public Task<Result<bool>> DeleteTaskAsync(Guid userId, Guid accountId, string listId, string taskId, CancellationToken ct = default) =>
        RunAsync(userId, accountId, async (token, c) =>
        {
            try
            {
                await SendAsync<JsonElement?>(token, HttpMethod.Delete, TaskPath(listId, taskId), null, c, TaskAnswer);
            }
            catch (GoogleTasksException ex) when (ex.Error.Code == GoogleTasksErrors.TaskNotFound.Code)
            {
                // Already gone: what the delete wanted.
            }
            return true;
        }, ct);

    private static string TaskPath(string listId, string taskId) =>
        $"lists/{Uri.EscapeDataString(listId)}/tasks/{Uri.EscapeDataString(taskId)}";

    private static Error? TaskAnswer(HttpStatusCode status) =>
        status is HttpStatusCode.NotFound or HttpStatusCode.Gone ? GoogleTasksErrors.TaskNotFound : null;

    private static string PageParam(string? pageToken) =>
        pageToken is null ? string.Empty : "&pageToken=" + Uri.EscapeDataString(pageToken);

    private async Task<Result<T>> RunAsync<T>(Guid userId, Guid accountId, Func<string, CancellationToken, Task<T>> work, CancellationToken ct)
    {
        var token = await credentials.GetAccessTokenAsync(userId, accountId, ct);
        if (!token.IsSuccess)
            return Result<T>.Failure(GoogleCalendarErrors.AuthFailed(string.Join("; ", token.Errors.Select(e => e.Message))));

        if (!token.Value!.Scopes.Contains(Scope, StringComparer.Ordinal))
            return Result<T>.Failure(GoogleTasksErrors.MissingScope);

        try
        {
            return Result<T>.Success(await work(token.Value.Token, ct));
        }
        catch (GoogleTasksException ex)
        {
            return Result<T>.Failure(ex.Error);
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            return Result<T>.Failure(GoogleCalendarErrors.Failed(ex.Message));
        }
    }

    private async Task<T> SendAsync<T>(
        string token, HttpMethod method, string path, object? body, CancellationToken ct, Func<HttpStatusCode, Error?> specific)
    {
        for (var attempt = 0; ; attempt++)
        {
            using var request = new HttpRequestMessage(method, BaseUrl + path);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            if (body is not null)
                request.Content = JsonContent.Create(body, body.GetType(), options: Json);
            using var response = await http.SendAsync(request, ct);

            if (response.StatusCode == HttpStatusCode.NoContent)
                return default!;
            if (response.IsSuccessStatusCode)
                return (await response.Content.ReadFromJsonAsync<T>(Json, ct))!;

            var text = await response.Content.ReadAsStringAsync(ct);
            var throttled = response.StatusCode == HttpStatusCode.TooManyRequests
                            || (response.StatusCode == HttpStatusCode.Forbidden && RateLimitReasons.Any(r => text.Contains(r, StringComparison.Ordinal)))
                            || (int)response.StatusCode >= 500;
            if (throttled && attempt < RetryDelays.Count)
            {
                await Task.Delay(response.Headers.RetryAfter?.Delta ?? RetryDelays[attempt], ct);
                continue;
            }

            throw new GoogleTasksException(specific(response.StatusCode) ?? ToError(response.StatusCode, text, path));
        }
    }

    private static Error ToError(HttpStatusCode status, string body, string path) => status switch
    {
        HttpStatusCode.Unauthorized => GoogleCalendarErrors.AuthFailed("Google rejected the access token."),
        HttpStatusCode.Forbidden when body.Contains("insufficientPermissions", StringComparison.Ordinal)
            || body.Contains("ACCESS_TOKEN_SCOPE_INSUFFICIENT", StringComparison.Ordinal) => GoogleTasksErrors.MissingScope,
        // The Cloud project behind the OAuth client has not enabled the Tasks API.
        HttpStatusCode.Forbidden when body.Contains("SERVICE_DISABLED", StringComparison.Ordinal)
            || body.Contains("accessNotConfigured", StringComparison.Ordinal) => GoogleTasksErrors.ApiDisabled,
        _ => GoogleCalendarErrors.Failed($"Google Tasks answered {(int)status} on {path.Split('?')[0]}.")
    };

    private sealed record Page<T>(IReadOnlyList<T>? Items, string? NextPageToken);

    private sealed class GoogleTasksException(Error error) : Exception(error.Message)
    {
        public Error Error { get; } = error;
    }
}
