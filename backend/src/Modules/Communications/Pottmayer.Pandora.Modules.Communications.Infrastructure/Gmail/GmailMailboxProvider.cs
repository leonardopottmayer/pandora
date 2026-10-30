using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using MimeKit;
using Pottmayer.Pandora.Modules.Communications.Domain.Entities;
using Pottmayer.Pandora.Modules.Communications.Domain.Errors;
using Pottmayer.Pandora.Modules.Communications.Domain.Ports;
using Pottmayer.Pandora.Modules.Communications.Domain.ValueObjects;
using Pottmayer.Pandora.Modules.Integrations.Abstractions.Ports;
using Pottmayer.Tars.Core.Primitives.Outcomes;

namespace Pottmayer.Pandora.Modules.Communications.Infrastructure.Gmail;

/// <summary>
/// Gmail through its REST API, with the access token of the mailbox's Google account. Owns the wire
/// mapping only: <c>UNREAD</c>/<c>STARRED</c> become flags, every other label id goes to the message's
/// label set, and <c>threadId</c> is the thread key.
/// </summary>
/// <remarks>
/// The sync cursor is Gmail's <c>historyId</c>; the backfill cursor is a <c>messages.list</c> page token
/// (<see cref="FirstPage"/> for the first page).
/// </remarks>
internal sealed class GmailMailboxProvider(HttpClient http, IExternalCredentialProvider credentials) : IMailboxProvider
{
    internal const string BaseUrl = "https://gmail.googleapis.com/gmail/v1/users/me/";
    internal const string FirstPage = "first";

    /// <summary>Read and change labels; <c>gmail.send</c> and <c>gmail.settings.basic</c> are M3/M4's.</summary>
    internal static readonly string[] AcceptedScopes =
        ["https://www.googleapis.com/auth/gmail.modify", "https://mail.google.com/"];

    // ponytail: one GET per message, two at a time — four drew frequent rate-limit 403s on a real
    // 64k-message backfill. Gmail's batch endpoint (100 per request) if the backfill is too slow.
    internal const int ParallelGets = 2;

    private static readonly string[] MetadataHeaders = ["From", "To", "Cc", "Subject", "Message-ID"];
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>A 404 on one message is an answer (it was deleted), not a failure.</summary>
    private static readonly Error MessageGone = MailErrors.ProviderFailed("Gmail message not found.");

    /// <summary>The 403 reasons Gmail uses for "slow down"; with 429 and 5xx they are retried after a pause.</summary>
    private static readonly string[] RateLimitReasons = ["rateLimitExceeded", "userRateLimitExceeded"];

    /// <summary>Pause before each retry of a throttled request; the server's <c>Retry-After</c> wins when sent.</summary>
    internal IReadOnlyList<TimeSpan> RetryDelays { get; init; } =
        [TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(4), TimeSpan.FromSeconds(8), TimeSpan.FromSeconds(16)];

    public MailboxCapabilities Capabilities { get; } = new(MultipleLabels: true, ServerFilters: true, NativeThreads: true);

    public Task<Result<bool>> TestConnectionAsync(Mailbox mailbox, CancellationToken ct = default) =>
        RunAsync(mailbox, async (token, c) =>
        {
            await GetAsync<Profile>(token, "profile", c);
            return true;
        }, ct);

    public Task<Result<IReadOnlyList<RemoteLabel>>> ListLabelsAsync(Mailbox mailbox, CancellationToken ct = default) =>
        RunAsync<IReadOnlyList<RemoteLabel>>(mailbox, async (token, c) =>
        {
            var response = await GetAsync<LabelList>(token, "labels", c);
            return response.Labels
                .Where(l => !IsFlag(l.Id))
                .Select(l => new RemoteLabel(
                    l.Id, l.Name, l.Type == "system" ? LabelKind.System : LabelKind.User,
                    LabelRole.FromGmailLabelId(l.Id), IsExclusive: false))
                .ToList();
        }, ct);

    public Task<Result<BackfillStart>> StartBackfillAsync(Mailbox mailbox, CancellationToken ct = default) =>
        RunAsync(mailbox, async (token, c) =>
        {
            var profile = await GetAsync<Profile>(token, "profile", c);
            return new BackfillStart(profile.HistoryId, profile.MessagesTotal > 0 ? FirstPage : null, profile.MessagesTotal);
        }, ct);

    public Task<Result<BackfillBatch>> FetchBackfillBatchAsync(
        Mailbox mailbox, string backfillCursor, int batchSize, CancellationToken ct = default) =>
        RunAsync(mailbox, async (token, c) =>
        {
            var url = $"messages?includeSpamTrash=true&maxResults={Math.Clamp(batchSize, 1, 500)}";
            if (backfillCursor != FirstPage)
                url += "&pageToken=" + Uri.EscapeDataString(backfillCursor);

            var page = await GetAsync<MessageList>(token, url, c);
            var messages = await GetMetadataAsync(token, (page.Messages ?? []).Select(m => m.Id), c);
            return new BackfillBatch(messages.Values.OrderByDescending(m => m.SentAt).ToList(), page.NextPageToken);
        }, ct);

    /// <summary>
    /// Every message the history touched is read again in full (a 404 means it is gone), so a label change
    /// and an arrival are the same thing to the engine: an upsert.
    /// </summary>
    public Task<Result<RemoteChanges>> FetchChangesAsync(Mailbox mailbox, string syncCursor, CancellationToken ct = default) =>
        RunAsync(mailbox, async (token, c) =>
        {
            var touched = new HashSet<string>();
            var deleted = new HashSet<string>();
            var cursor = syncCursor;
            string? pageToken = null;

            do
            {
                var url = $"history?startHistoryId={Uri.EscapeDataString(syncCursor)}";
                if (pageToken is not null)
                    url += "&pageToken=" + Uri.EscapeDataString(pageToken);

                var page = await GetAsync<HistoryList>(token, url, c, notFound: MailErrors.SyncCursorExpired);
                foreach (var record in page.History ?? [])
                {
                    foreach (var m in record.Messages ?? [])
                        touched.Add(m.Id);
                    foreach (var d in record.MessagesDeleted ?? [])
                        deleted.Add(d.Message.Id);
                }

                cursor = page.HistoryId ?? cursor;
                pageToken = page.NextPageToken;
            } while (pageToken is not null);

            touched.ExceptWith(deleted);
            var current = await GetMetadataAsync(token, touched, c);
            deleted.UnionWith(touched.Where(id => !current.ContainsKey(id)));

            return new RemoteChanges(current.Values.ToList(), deleted.ToList(), [], cursor);
        }, ct);

    public Task<Result<byte[]>> GetRawMessageAsync(Mailbox mailbox, string providerMessageId, CancellationToken ct = default) =>
        RunAsync(mailbox, async (token, c) =>
        {
            var message = await GetAsync<RawMessage>(token, $"messages/{Uri.EscapeDataString(providerMessageId)}?format=raw", c);
            return DecodeBase64Url(message.Raw);
        }, ct);

    /// <summary>Gets the token, checks the scope, runs <paramref name="work"/> and maps Gmail's answers to the port's errors.</summary>
    private async Task<Result<T>> RunAsync<T>(Mailbox mailbox, Func<string, CancellationToken, Task<T>> work, CancellationToken ct)
    {
        var token = await credentials.GetAccessTokenAsync(mailbox.UserId, mailbox.ExternalAccountId, ct);
        if (!token.IsSuccess)
            return Result<T>.Failure(MailErrors.MailboxAuthFailed(string.Join("; ", token.Errors.Select(e => e.Message))));

        if (!token.Value.Scopes.Any(s => AcceptedScopes.Contains(s)))
            return Result<T>.Failure(MailErrors.MissingScopes);

        try
        {
            return Result<T>.Success(await work(token.Value.Token, ct));
        }
        catch (GmailException ex)
        {
            return Result<T>.Failure(ex.Error);
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            return Result<T>.Failure(MailErrors.ProviderFailed(ex.Message));
        }
    }

    private async Task<T> GetAsync<T>(string token, string path, CancellationToken ct, Error? notFound = null)
    {
        for (var attempt = 0; ; attempt++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, BaseUrl + path);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using var response = await http.SendAsync(request, ct);

            if (response.IsSuccessStatusCode)
                return (await response.Content.ReadFromJsonAsync<T>(Json, ct))!;

            var body = await response.Content.ReadAsStringAsync(ct);
            var throttled = response.StatusCode is HttpStatusCode.TooManyRequests
                            || (int)response.StatusCode >= 500
                            || (response.StatusCode == HttpStatusCode.Forbidden && RateLimitReasons.Any(r => body.Contains(r, StringComparison.Ordinal)));
            if (throttled && attempt < RetryDelays.Count)
            {
                await Task.Delay(response.Headers.RetryAfter?.Delta ?? RetryDelays[attempt], ct);
                continue;
            }

            throw new GmailException(response.StatusCode switch
            {
                HttpStatusCode.Unauthorized => MailErrors.MailboxAuthFailed("Gmail rejected the access token."),
                HttpStatusCode.Forbidden when body.Contains("insufficientPermissions", StringComparison.Ordinal)
                    || body.Contains("ACCESS_TOKEN_SCOPE_INSUFFICIENT", StringComparison.Ordinal) => MailErrors.MissingScopes,
                HttpStatusCode.NotFound when notFound is not null => notFound,
                _ => MailErrors.ProviderFailed($"Gmail answered {(int)response.StatusCode}{Reason(body)} on {path.Split('?')[0]}.")
            });
        }
    }

    /// <summary>Gmail's own reason for an error (<c>error.errors[0].reason</c>, else <c>error.status</c>), for the mailbox's last error.</summary>
    private static string Reason(string body)
    {
        try
        {
            using var json = JsonDocument.Parse(body);
            var error = json.RootElement.GetProperty("error");
            var reason = error.TryGetProperty("errors", out var errors) && errors.GetArrayLength() > 0
                         && errors[0].TryGetProperty("reason", out var r) ? r.GetString()
                : error.TryGetProperty("status", out var status) ? status.GetString()
                : null;
            return reason is null ? string.Empty : $" ({reason})";
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            return string.Empty;
        }
    }

    /// <summary>The messages that still exist, by id; a 404 leaves the id out.</summary>
    private async Task<Dictionary<string, RemoteMessage>> GetMetadataAsync(
        string token, IEnumerable<string> ids, CancellationToken ct)
    {
        var headers = string.Concat(MetadataHeaders.Select(h => "&metadataHeaders=" + h));
        var found = new ConcurrentDictionary<string, RemoteMessage>();

        await Parallel.ForEachAsync(ids, new ParallelOptions { MaxDegreeOfParallelism = ParallelGets, CancellationToken = ct },
            async (id, c) =>
            {
                try
                {
                    var message = await GetAsync<Message>(
                        token, $"messages/{Uri.EscapeDataString(id)}?format=metadata{headers}", c, notFound: MessageGone);
                    found[id] = ToRemote(message);
                }
                catch (GmailException ex) when (ReferenceEquals(ex.Error, MessageGone))
                {
                }
            });

        return new Dictionary<string, RemoteMessage>(found);
    }

    internal static RemoteMessage ToRemote(Message message)
    {
        var headers = (message.Payload?.Headers ?? [])
            .GroupBy(h => h.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Value, StringComparer.OrdinalIgnoreCase);
        var labels = message.LabelIds ?? [];

        return new RemoteMessage(
            ProviderMessageId: message.Id,
            ThreadKey: message.ThreadId,
            Rfc822MessageId: headers.TryGetValue("Message-ID", out var messageId) ? messageId.Trim().Trim('<', '>') : null,
            From: Addresses(headers.GetValueOrDefault("From")).FirstOrDefault(),
            To: Addresses(headers.GetValueOrDefault("To")),
            Cc: Addresses(headers.GetValueOrDefault("Cc")),
            Subject: headers.GetValueOrDefault("Subject") ?? string.Empty,
            Snippet: WebUtility.HtmlDecode(message.Snippet ?? string.Empty),
            SentAt: DateTimeOffset.FromUnixTimeMilliseconds(long.Parse(message.InternalDate, CultureInfo.InvariantCulture)),
            IsUnread: labels.Contains("UNREAD"),
            IsStarred: labels.Contains("STARRED"),
            HasAttachments: string.Equals(message.Payload?.MimeType, "multipart/mixed", StringComparison.OrdinalIgnoreCase),
            SizeBytes: message.SizeEstimate,
            ProviderLabelIds: labels.Where(l => !IsFlag(l)).ToList());
    }

    private static List<MailAddress> Addresses(string? header) =>
        header is not null && InternetAddressList.TryParse(header, out var list)
            ? list.Mailboxes.Select(m => new MailAddress(string.IsNullOrWhiteSpace(m.Name) ? null : m.Name, m.Address)).ToList()
            : [];

    private static bool IsFlag(string labelId) => labelId is "UNREAD" or "STARRED";

    private static byte[] DecodeBase64Url(string value)
    {
        var base64 = value.Replace('-', '+').Replace('_', '/');
        return Convert.FromBase64String(base64.PadRight(base64.Length + (4 - base64.Length % 4) % 4, '='));
    }

    private sealed class GmailException(Error error) : Exception(error.Message)
    {
        public Error Error { get; } = error;
    }

    // Wire shapes (only the fields read).
    internal sealed record Profile(string EmailAddress, int MessagesTotal, string HistoryId);
    internal sealed record LabelList(List<Label> Labels);
    internal sealed record Label(string Id, string Name, string? Type);
    internal sealed record MessageList(List<MessageRef>? Messages, string? NextPageToken);
    internal sealed record MessageRef(string Id);
    internal sealed record HistoryList(List<HistoryRecord>? History, string? NextPageToken, string? HistoryId);
    internal sealed record HistoryRecord(List<MessageRef>? Messages, List<MessageDeleted>? MessagesDeleted);
    internal sealed record MessageDeleted(MessageRef Message);
    internal sealed record Message(
        string Id, string ThreadId, List<string>? LabelIds, string? Snippet, long SizeEstimate, string InternalDate, Payload? Payload);
    internal sealed record Payload(string? MimeType, List<Header>? Headers);
    internal sealed record Header(string Name, string Value);
    internal sealed record RawMessage(string Raw);
}
