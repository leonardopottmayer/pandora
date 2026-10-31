using System.Globalization;
using MimeKit;
using Pottmayer.Pandora.Modules.Communications.Domain.Entities;
using Pottmayer.Pandora.Modules.Communications.Domain.Errors;
using Pottmayer.Pandora.Modules.Communications.Domain.Ports;
using Pottmayer.Pandora.Modules.Communications.Domain.ValueObjects;
using Pottmayer.Tars.Core.Primitives.Outcomes;

namespace Pottmayer.Pandora.IntegrationTests.Modules.Communications;

/// <summary>
/// A mail server the test scripts: what the mailbox holds, what changed since the last call, and the
/// error to answer with. Changes reach the engine once, like a provider's history.
/// </summary>
internal sealed class FakeMailProvider : IMailboxProvider
{
    private readonly Dictionary<string, RemoteMessage> _messages = [];
    private readonly List<string> _changed = [];
    private readonly List<string> _removed = [];
    private int _version;

    public List<RemoteLabel> Labels { get; } = [new("INBOX", "INBOX", LabelKind.System, LabelRole.Inbox, IsExclusive: false)];

    /// <summary>Every call answers with this error while it is set.</summary>
    public Error? FailWith { get; set; }

    /// <summary>Every call throws this while it is set.</summary>
    public Exception? ThrowWith { get; set; }

    /// <summary>The next FetchChanges answers SyncCursorExpired once.</summary>
    public bool ExpireCursorOnce { get; set; }

    /// <summary>Send the label's whole id set instead of removed ids (the IMAP way).</summary>
    public string? SnapshotLabel { get; set; }

    /// <summary>Awaited at the start of FetchChanges and of each backfill batch, to hold a cycle there.</summary>
    public Func<Task>? BeforeFetchChanges { get; set; }

    public Func<Task>? BeforeBackfillBatch { get; set; }

    public int StartBackfillCalls { get; private set; }

    public int RawFetches { get; private set; }

    public MailboxCapabilities Capabilities { get; } = new(true, false, false);

    public RemoteMessage Deliver(
        string id, string subject, string? rfc822 = null, DateTimeOffset? sentAt = null, bool unread = true,
        string label = "INBOX", bool noMessageId = false)
    {
        var message = new RemoteMessage(
            id, $"thread-{id}", noMessageId ? null : rfc822 ?? $"{id}@mail.test", new MailAddress("Ana", "ana@mail.test"),
            [new MailAddress(null, "me@mail.test")], [], subject, $"About {subject}",
            sentAt ?? DateTimeOffset.UtcNow, unread, false, false, 100, [label]);
        _messages[id] = message;
        _changed.Add(id);
        return message;
    }

    /// <summary>What a move does on IMAP: a new id under another label, and the old id gone.</summary>
    public void Move(string id, string newId, string label)
    {
        var moved = _messages[id] with { ProviderMessageId = newId, ProviderLabelIds = [label] };
        _messages.Remove(id);
        _messages[newId] = moved;
        _changed.Add(newId);
    }

    public void MarkRead(string id)
    {
        _messages[id] = _messages[id] with { IsUnread = false };
        _changed.Add(id);
    }

    public void Delete(string id)
    {
        _messages.Remove(id);
        _removed.Add(id);
    }

    public Task<Result<bool>> TestConnectionAsync(Mailbox mailbox, CancellationToken ct = default) => Answer(true);

    public Task<Result<IReadOnlyList<RemoteLabel>>> ListLabelsAsync(Mailbox mailbox, CancellationToken ct = default) =>
        Answer<IReadOnlyList<RemoteLabel>>(Labels.ToList());

    public Task<Result<BackfillStart>> StartBackfillAsync(Mailbox mailbox, CancellationToken ct = default)
    {
        StartBackfillCalls++;
        _changed.Clear();
        _removed.Clear();
        return Answer(new BackfillStart($"v{_version}", _messages.Count > 0 ? "0" : null, _messages.Count));
    }

    public async Task<Result<BackfillBatch>> FetchBackfillBatchAsync(
        Mailbox mailbox, string backfillCursor, int batchSize, CancellationToken ct = default)
    {
        if (BeforeBackfillBatch is { } hold)
            await hold();

        var offset = int.Parse(backfillCursor, CultureInfo.InvariantCulture);
        var page = _messages.Values.OrderByDescending(m => m.SentAt).Skip(offset).Take(batchSize).ToList();
        var next = offset + page.Count < _messages.Count ? (offset + page.Count).ToString(CultureInfo.InvariantCulture) : null;
        return await Answer(new BackfillBatch(page, next));
    }

    public async Task<Result<RemoteChanges>> FetchChangesAsync(Mailbox mailbox, string syncCursor, CancellationToken ct = default)
    {
        if (BeforeFetchChanges is { } hold)
            await hold();

        if (ExpireCursorOnce)
        {
            ExpireCursorOnce = false;
            return Result<RemoteChanges>.Failure(MailErrors.SyncCursorExpired);
        }

        var upserts = _changed.Distinct().Where(_messages.ContainsKey).Select(id => _messages[id]).ToList();
        IReadOnlyList<string> removed = SnapshotLabel is null ? [.. _removed] : [];
        IReadOnlyList<RemoteLabelSnapshot> snapshots = SnapshotLabel is null
            ? []
            : [new RemoteLabelSnapshot(SnapshotLabel, _messages.Values.Where(m => m.ProviderLabelIds.Contains(SnapshotLabel)).Select(m => m.ProviderMessageId).ToList())];
        _changed.Clear();
        _removed.Clear();
        return await Answer(new RemoteChanges(upserts, removed, snapshots, $"v{++_version}"));
    }

    /// <summary>A text + HTML body and one PDF attachment, whatever the message.</summary>
    public Task<Result<byte[]>> GetRawMessageAsync(Mailbox mailbox, string providerMessageId, CancellationToken ct = default)
    {
        RawFetches++;
        var subject = _messages.TryGetValue(providerMessageId, out var known) ? known.Subject : providerMessageId;
        var body = new BodyBuilder { TextBody = $"Text of {subject}", HtmlBody = $"<p>Html of {subject}</p>" };
        body.Attachments.Add("invoice.pdf", "%PDF-1.4 fake"u8.ToArray(), new ContentType("application", "pdf"));
        var message = new MimeMessage { Subject = subject, Body = body.ToMessageBody() };
        message.From.Add(new MailboxAddress("Ana", "ana@mail.test"));

        using var stream = new MemoryStream();
        message.WriteTo(stream);
        return Answer(stream.ToArray());
    }

    private Task<Result<T>> Answer<T>(T value)
    {
        if (ThrowWith is { } exception)
            throw exception;

        return Task.FromResult(FailWith is { } error ? Result<T>.Failure(error) : Result<T>.Success(value));
    }
}
