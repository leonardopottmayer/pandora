using System.Net;
using System.Text;
using System.Text.Json;
using Pottmayer.Pandora.Modules.Communications.Domain.Entities;
using Pottmayer.Pandora.Modules.Communications.Domain.Ports;
using Pottmayer.Pandora.Modules.Communications.Domain.ValueObjects;
using Pottmayer.Pandora.Modules.Communications.Infrastructure.Gmail;
using Pottmayer.Pandora.Modules.Integrations.Abstractions.Models;
using Pottmayer.Pandora.Modules.Integrations.Abstractions.Ports;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Xunit;

namespace Pottmayer.Pandora.Modules.Communications.Tests;

/// <summary>The Gmail adapter against canned Gmail API answers (no real Google in automated tests).</summary>
public sealed class GmailMailboxProviderTests
{
    private const string Modify = "https://www.googleapis.com/auth/gmail.modify";

    private readonly Mailbox _mailbox =
        Mailbox.Connect(Guid.NewGuid(), MailProvider.Gmail, Guid.NewGuid(), "me@gmail.com", null);

    [Fact]
    public async Task Labels_leave_out_the_flag_labels_and_map_roles()
    {
        var gmail = Provider(Routes(("labels", Ok("""
            { "labels": [
                { "id": "INBOX", "name": "INBOX", "type": "system" },
                { "id": "UNREAD", "name": "UNREAD", "type": "system" },
                { "id": "STARRED", "name": "STARRED", "type": "system" },
                { "id": "Label_7", "name": "Receipts", "type": "user" } ] }
            """))));

        var labels = Value(await gmail.ListLabelsAsync(_mailbox));

        Assert.Equal(["INBOX", "Label_7"], labels.Select(l => l.ProviderLabelId));
        Assert.Equal(LabelRole.Inbox, labels[0].Role);
        Assert.Equal(LabelKind.System, labels[0].Kind);
        Assert.Equal(LabelKind.User, labels[1].Kind);
        Assert.All(labels, l => Assert.False(l.IsExclusive));
    }

    [Fact]
    public async Task Backfill_reads_pages_of_metadata_newest_first()
    {
        var requests = new List<string>();
        var gmail = Provider(Routes(
            ("profile", Ok("""{ "emailAddress": "me@gmail.com", "messagesTotal": 2, "historyId": "100" }""")),
            ("messages?", Ok("""{ "messages": [ { "id": "m1" }, { "id": "m2" } ], "nextPageToken": "p2" }""")),
            ("messages/m1", Ok(MessageJson("m1", "t1", 1_000, ["INBOX", "UNREAD", "CATEGORY_UPDATES"], "multipart/mixed"))),
            ("messages/m2", Ok(MessageJson("m2", "t1", 2_000, ["INBOX", "STARRED"], "text/plain")))), requests);

        var start = Value(await gmail.StartBackfillAsync(_mailbox));
        Assert.Equal(new BackfillStart("100", GmailMailboxProvider.FirstPage, 2), start);

        var batch = Value(await gmail.FetchBackfillBatchAsync(_mailbox, start.BackfillCursor!, 50));

        Assert.Equal("p2", batch.NextCursor);
        Assert.Equal(["m2", "m1"], batch.Messages.Select(m => m.ProviderMessageId));
        Assert.DoesNotContain(requests, r => r.Contains("pageToken"));

        var m1 = batch.Messages[1];
        Assert.Equal("t1", m1.ThreadKey);
        Assert.Equal("abc@mail.test", m1.Rfc822MessageId);
        Assert.Equal(new MailAddress("Ana", "ana@mail.test"), m1.From);
        Assert.Equal(["me@gmail.com", "bo@mail.test"], m1.To.Select(a => a.Address));
        Assert.Equal("Hi m1", m1.Subject);
        Assert.Equal("It's here & ready", m1.Snippet);
        Assert.Equal(DateTimeOffset.FromUnixTimeMilliseconds(1_000), m1.SentAt);
        Assert.True(m1.IsUnread);
        Assert.False(m1.IsStarred);
        Assert.True(m1.HasAttachments);
        Assert.Equal(["INBOX", "CATEGORY_UPDATES"], m1.ProviderLabelIds);

        var m2 = batch.Messages[0];
        Assert.False(m2.IsUnread);
        Assert.True(m2.IsStarred);
        Assert.False(m2.HasAttachments);
    }

    [Fact]
    public async Task Next_backfill_page_sends_the_page_token()
    {
        var requests = new List<string>();
        var gmail = Provider(Routes(("messages?", Ok("""{ "messages": [] }"""))), requests);

        var batch = Value(await gmail.FetchBackfillBatchAsync(_mailbox, "p2", 50));

        Assert.Null(batch.NextCursor);
        Assert.Contains("pageToken=p2", Assert.Single(requests));
    }

    [Fact]
    public async Task An_empty_mailbox_has_no_backfill()
    {
        var gmail = Provider(Routes(("profile", Ok("""{ "emailAddress": "me@gmail.com", "messagesTotal": 0, "historyId": "5" }"""))));

        Assert.Null(Value(await gmail.StartBackfillAsync(_mailbox)).BackfillCursor);
    }

    [Fact]
    public async Task Changes_reread_touched_messages_across_history_pages()
    {
        var requests = new List<string>();
        var gmail = Provider(Routes(
            ("history?startHistoryId=100&pageToken=h2", Ok("""{ "history": [ { "messages": [ { "id": "m4" } ] } ], "historyId": "160" }""")),
            ("history?startHistoryId=100", Ok("""
                { "history": [
                    { "messages": [ { "id": "m1" }, { "id": "m2" }, { "id": "m3" } ] },
                    { "messagesDeleted": [ { "message": { "id": "m3" } } ] } ],
                  "nextPageToken": "h2", "historyId": "150" }
                """)),
            ("messages/m1", Ok(MessageJson("m1", "t1", 1_000, ["INBOX"], "text/plain"))),
            ("messages/m2", NotFound()),
            ("messages/m4", Ok(MessageJson("m4", "t4", 4_000, ["INBOX", "UNREAD"], "text/plain")))), requests);

        var changes = Value(await gmail.FetchChangesAsync(_mailbox, "100"));

        Assert.Equal(["m1", "m4"], changes.Upserts.Select(m => m.ProviderMessageId).Order());
        Assert.Equal(["m2", "m3"], changes.RemovedIds.Order());
        Assert.Empty(changes.Snapshots);
        Assert.Equal("160", changes.SyncCursor);
        Assert.DoesNotContain(requests, r => r.Contains("messages/m3"));
    }

    [Fact]
    public async Task No_history_keeps_the_new_history_id()
    {
        var gmail = Provider(Routes(("history?", Ok("""{ "historyId": "120" }"""))));

        var changes = Value(await gmail.FetchChangesAsync(_mailbox, "100"));

        Assert.Empty(changes.Upserts);
        Assert.Equal("120", changes.SyncCursor);
    }

    [Fact]
    public async Task Expired_history_is_a_cursor_expired_error()
    {
        var gmail = Provider(Routes(("history?", NotFound())));

        AssertError("Communications.SyncCursorExpired", await gmail.FetchChangesAsync(_mailbox, "1"));
    }

    [Fact]
    public async Task A_token_without_gmail_scopes_is_rejected_before_calling_gmail()
    {
        var requests = new List<string>();
        var gmail = Provider(Routes(), requests, scopes: ["openid", "email"]);

        AssertError("Communications.MissingScopes", await gmail.TestConnectionAsync(_mailbox));
        Assert.Empty(requests);
    }

    [Fact]
    public async Task Gmail_answers_map_to_port_errors()
    {
        AssertError("Communications.MailboxAuthFailed",
            await Provider(Routes(("profile", Status(HttpStatusCode.Unauthorized, "{}")))).TestConnectionAsync(_mailbox));
        AssertError("Communications.MissingScopes",
            await Provider(Routes(("profile", Status(HttpStatusCode.Forbidden, """{"error":{"status":"PERMISSION_DENIED","details":[{"reason":"ACCESS_TOKEN_SCOPE_INSUFFICIENT"}]}}"""))))
                .TestConnectionAsync(_mailbox));
        AssertError("Communications.ProviderFailed",
            await Provider(Routes(("profile", Status(HttpStatusCode.TooManyRequests, "{}")))).TestConnectionAsync(_mailbox));
    }

    [Fact]
    public async Task A_throttled_request_is_retried_and_a_lasting_one_names_the_reason()
    {
        const string RateLimited = """{"error":{"code":403,"errors":[{"reason":"userRateLimitExceeded"}]}}""";
        var answers = new Queue<Func<HttpResponseMessage>>([Status(HttpStatusCode.Forbidden, RateLimited), Ok("""{ "raw": "SGk" }""")]);
        var requests = new List<string>();

        var recovered = Provider(_ => answers.Dequeue()(), requests);
        Assert.Equal("Hi"u8.ToArray(), Value(await recovered.GetRawMessageAsync(_mailbox, "m1")));
        Assert.Equal(2, requests.Count);

        var failed = await Provider(Routes(("messages/m1", Status(HttpStatusCode.Forbidden, RateLimited)))).GetRawMessageAsync(_mailbox, "m1");
        AssertError("Communications.ProviderFailed", failed);
        Assert.Contains("userRateLimitExceeded", failed.Errors[0].Message);
    }

    [Fact]
    public async Task A_credential_failure_is_an_auth_failure()
    {
        var gmail = new GmailMailboxProvider(new HttpClient(new FakeHandler(Routes())), new FakeCredentials(null));

        AssertError("Communications.MailboxAuthFailed", await gmail.TestConnectionAsync(_mailbox));
    }

    [Fact]
    public async Task Raw_message_is_decoded_from_base64url()
    {
        var mime = "Subject: Hi?\r\n\r\n>>>???"u8.ToArray();
        var raw = Convert.ToBase64String(mime).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var gmail = Provider(Routes(("messages/m1?format=raw", Ok($$"""{ "raw": "{{raw}}" }"""))));

        Assert.Equal(mime, Value(await gmail.GetRawMessageAsync(_mailbox, "m1")));
    }

    [Fact]
    public async Task Flags_become_the_unread_and_starred_labels_in_one_batch_call()
    {
        var calls = new List<Call>();
        var gmail = Provider(Routes(("messages/batchModify", NoContent())), calls: calls);

        Value(await gmail.SetFlagsAsync(_mailbox, ["m1", "m2"], isUnread: false, isStarred: true));

        var call = Assert.Single(calls);
        Assert.Equal("POST", call.Method);
        Assert.EndsWith("/messages/batchModify", call.Url);
        Assert.Equal(["m1", "m2"], Strings(call.Body, "ids"));
        Assert.Equal(["STARRED"], Strings(call.Body, "addLabelIds"));
        Assert.Equal(["UNREAD"], Strings(call.Body, "removeLabelIds"));
    }

    [Fact]
    public async Task Nothing_to_change_makes_no_call()
    {
        var calls = new List<Call>();
        var gmail = Provider(Routes(), calls: calls);

        Value(await gmail.SetFlagsAsync(_mailbox, ["m1"], isUnread: null, isStarred: null));
        Value(await gmail.ChangeLabelsAsync(_mailbox, [], ["Label_1"], []));

        Assert.Empty(calls);
    }

    [Fact]
    public async Task Labels_change_as_asked()
    {
        var calls = new List<Call>();
        var gmail = Provider(Routes(("messages/batchModify", NoContent())), calls: calls);

        Value(await gmail.ChangeLabelsAsync(_mailbox, ["m1"], ["Label_1"], ["Label_2"]));

        var body = Assert.Single(calls).Body;
        Assert.Equal(["Label_1"], Strings(body, "addLabelIds"));
        Assert.Equal(["Label_2"], Strings(body, "removeLabelIds"));
    }

    [Fact]
    public async Task A_move_takes_the_label_leaves_the_inbox_and_keeps_the_id()
    {
        var calls = new List<Call>();
        var gmail = Provider(Routes(("messages/batchModify", NoContent())), calls: calls);

        var moved = Value(await gmail.MoveAsync(_mailbox, ["m1"], "Label_1"));

        Assert.Equal("m1", moved["m1"]);
        var body = Assert.Single(calls).Body;
        Assert.Equal(["Label_1"], Strings(body, "addLabelIds"));
        Assert.Equal(["INBOX"], Strings(body, "removeLabelIds"));
    }

    [Fact]
    public async Task A_move_to_the_inbox_removes_nothing()
    {
        var calls = new List<Call>();
        var gmail = Provider(Routes(("messages/batchModify", NoContent())), calls: calls);

        Value(await gmail.MoveAsync(_mailbox, ["m1"], "INBOX"));

        Assert.Empty(Strings(Assert.Single(calls).Body, "removeLabelIds"));
    }

    [Fact]
    public async Task Trash_calls_trash_per_message_counts_a_gone_one_as_done_and_never_deletes()
    {
        var calls = new List<Call>();
        var gmail = Provider(Routes(
            ("messages/m1/trash", Ok(MessageJson("m1", "t1", 1, ["TRASH"], "text/plain"))),
            ("messages/m2/trash", NotFound())), calls: calls);

        var trashed = Value(await gmail.TrashAsync(_mailbox, ["m1", "m2"]));

        Assert.Equal(["m1", "m2"], trashed.Keys.Order());
        Assert.All(calls, c =>
        {
            Assert.Equal("POST", c.Method);
            Assert.EndsWith("/trash", c.Url);
        });
        Assert.Equal(2, calls.Count);
    }

    [Fact]
    public async Task Labels_are_created_renamed_and_deleted()
    {
        var calls = new List<Call>();
        var gmail = Provider(url => url.EndsWith("/labels", StringComparison.Ordinal)
            ? Ok("""{ "id": "Label_9", "name": "pandora-test", "type": "user" }""")()
            : NoContent()(), calls: calls);

        var created = Value(await gmail.CreateLabelAsync(_mailbox, "pandora-test"));
        Value(await gmail.RenameLabelAsync(_mailbox, "Label_9", "renamed"));
        Value(await gmail.DeleteLabelAsync(_mailbox, "Label_9"));

        Assert.Equal(new RemoteLabel("Label_9", "pandora-test", LabelKind.User, null, IsExclusive: false), created);
        Assert.Equal(["POST", "PATCH", "DELETE"], calls.Select(c => c.Method));
        Assert.Equal("pandora-test", Text(calls[0].Body, "name"));
        Assert.EndsWith("/labels/Label_9", calls[1].Url);
        Assert.Equal("renamed", Text(calls[1].Body, "name"));
        Assert.EndsWith("/labels/Label_9", calls[2].Url);
    }

    [Fact]
    public async Task A_throttled_write_is_sent_again_with_its_body()
    {
        const string RateLimited = """{"error":{"code":403,"errors":[{"reason":"rateLimitExceeded"}]}}""";
        var answers = new Queue<Func<HttpResponseMessage>>([Status(HttpStatusCode.Forbidden, RateLimited), NoContent()]);
        var calls = new List<Call>();
        var gmail = Provider(_ => answers.Dequeue()(), calls: calls);

        Value(await gmail.SetFlagsAsync(_mailbox, ["m1"], isUnread: false, isStarred: null));

        Assert.Equal(2, calls.Count);
        Assert.Equal(["m1"], Strings(calls[1].Body, "ids"));
    }

    [Fact]
    public async Task A_write_without_the_scope_is_a_missing_scope()
    {
        var refused = Provider(Routes(("messages/batchModify", Status(HttpStatusCode.Forbidden,
            """{"error":{"status":"PERMISSION_DENIED","details":[{"reason":"ACCESS_TOKEN_SCOPE_INSUFFICIENT"}]}}"""))));
        AssertError("Communications.MissingScopes", await refused.MoveAsync(_mailbox, ["m1"], "Label_1"));

        var calls = new List<Call>();
        var noScope = Provider(Routes(), scopes: ["openid"], calls: calls);
        AssertError("Communications.MissingScopes", await noScope.TrashAsync(_mailbox, ["m1"]));
        Assert.Empty(calls);
    }

    private static string[] Strings(string? json, string property)
    {
        using var document = JsonDocument.Parse(json!);
        return document.RootElement.GetProperty(property).EnumerateArray().Select(e => e.GetString()!).ToArray();
    }

    private static string? Text(string? json, string property)
    {
        using var document = JsonDocument.Parse(json!);
        return document.RootElement.GetProperty(property).GetString();
    }

    private static string MessageJson(string id, string threadId, long internalDate, string[] labels, string mimeType) => $$"""
        { "id": "{{id}}", "threadId": "{{threadId}}", "labelIds": [{{string.Join(", ", labels.Select(l => $"\"{l}\""))}}],
          "snippet": "It&#39;s here &amp; ready", "sizeEstimate": 1234, "internalDate": "{{internalDate}}",
          "payload": { "mimeType": "{{mimeType}}", "headers": [
            { "name": "From", "value": "Ana <ana@mail.test>" },
            { "name": "To", "value": "me@gmail.com, Bo <bo@mail.test>" },
            { "name": "Subject", "value": "Hi {{id}}" },
            { "name": "Message-Id", "value": "<abc@mail.test>" } ] } }
        """;

    private static GmailMailboxProvider Provider(
        Func<string, HttpResponseMessage> routes, List<string>? requests = null, string[]? scopes = null, List<Call>? calls = null) =>
        new(new HttpClient(new FakeHandler(routes, requests, calls)), new FakeCredentials(scopes ?? ["openid", Modify]))
        {
            RetryDelays = [TimeSpan.Zero, TimeSpan.Zero],
        };

    /// <summary>First route whose key appears in the request's path and query wins; unknown → 500.</summary>
    private static Func<string, HttpResponseMessage> Routes(params (string Key, Func<HttpResponseMessage> Response)[] routes) =>
        url => routes.FirstOrDefault(r => url.Contains(r.Key, StringComparison.Ordinal)).Response?.Invoke()
               ?? new HttpResponseMessage(HttpStatusCode.InternalServerError);

    private static Func<HttpResponseMessage> Ok(string json) => Status(HttpStatusCode.OK, json);
    private static Func<HttpResponseMessage> NotFound() => Status(HttpStatusCode.NotFound, "{}");
    private static Func<HttpResponseMessage> NoContent() => () => new HttpResponseMessage(HttpStatusCode.NoContent);

    private static Func<HttpResponseMessage> Status(HttpStatusCode status, string json) =>
        () => new HttpResponseMessage(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private static T Value<T>(Result<T> result)
    {
        Assert.True(result.IsSuccess, string.Join("; ", result.Errors.Select(e => $"{e.Code}: {e.Message}")));
        return result.Value;
    }

    private static void AssertError<T>(string code, Result<T> result)
    {
        Assert.False(result.IsSuccess);
        Assert.Equal(code, Assert.Single(result.Errors).Code);
    }

    private sealed record Call(string Method, string Url, string? Body);

    private sealed class FakeHandler(
        Func<string, HttpResponseMessage> routes, List<string>? requests = null, List<Call>? calls = null) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
            var url = request.RequestUri!.PathAndQuery;
            var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(ct);
            lock (this)
            {
                requests?.Add(url);
                calls?.Add(new Call(request.Method.Method, url, body));
            }
            return routes(url);
        }
    }

    /// <summary>A Google account token with <paramref name="scopes"/>; null scopes = no usable account.</summary>
    private sealed class FakeCredentials(string[]? scopes) : IExternalCredentialProvider
    {
        public Task<Result<ExternalAccessToken>> GetAccessTokenAsync(Guid userId, Guid accountId, CancellationToken ct = default) =>
            Task.FromResult(scopes is null
                ? Result<ExternalAccessToken>.Failure(Error.NotFound("Integrations.NotConnected", "No account."))
                : Result<ExternalAccessToken>.Success(new ExternalAccessToken("token", DateTimeOffset.MaxValue, scopes)));

        public Task<Result<string>> GetApiKeyAsync(Guid userId, Guid accountId, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<Result<PasswordCredential>> GetPasswordAsync(Guid userId, Guid accountId, CancellationToken ct = default) =>
            throw new NotSupportedException();
    }
}
