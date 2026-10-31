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

    public MailboxCapabilities Capabilities { get; set; } = new(true, false, false);

    /// <summary>Every write, in order: <c>"move:m1:Archive"</c>.</summary>
    public List<string> Writes { get; } = [];

    /// <summary>Every message sent, in order.</summary>
    public List<(byte[] Raw, string? ThreadKey)> Sent { get; } = [];

    public bool CopySaved { get; set; } = true;

    /// <summary>The id a move gives a message; null is a provider that does not say. By default the id stays.</summary>
    public Func<string, string?> MovedId { get; set; } = id => id;

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
        if (known?.Rfc822MessageId is { } messageId)
            message.MessageId = messageId;
        message.From.Add(new MailboxAddress("Ana", "ana@mail.test"));

        using var stream = new MemoryStream();
        message.WriteTo(stream);
        return Answer(stream.ToArray());
    }

    public Task<Result<RemoteLabel>> CreateLabelAsync(Mailbox mailbox, string name, CancellationToken ct = default)
    {
        if (FailWith is not null)
            return Answer<RemoteLabel>(null!);

        Writes.Add($"create:{name}");
        var label = new RemoteLabel(name, name, LabelKind.User, null, IsExclusive: !Capabilities.MultipleLabels);
        Labels.Add(label);
        return Answer(label);
    }

    /// <summary>The way Gmail does it: the id stays. An IMAP rename is a folder gone and one new.</summary>
    public Task<Result<bool>> RenameLabelAsync(Mailbox mailbox, string providerLabelId, string newName, CancellationToken ct = default)
    {
        if (FailWith is not null)
            return Answer(false);

        Writes.Add($"rename:{providerLabelId}:{newName}");
        var index = Labels.FindIndex(l => l.ProviderLabelId == providerLabelId);
        Labels[index] = Labels[index] with { Name = newName };
        return Answer(true);
    }

    public Task<Result<bool>> DeleteLabelAsync(Mailbox mailbox, string providerLabelId, CancellationToken ct = default)
    {
        if (FailWith is not null)
            return Answer(false);

        Writes.Add($"delete:{providerLabelId}");
        Labels.RemoveAll(l => l.ProviderLabelId == providerLabelId);
        return Answer(true);
    }

    public Task<Result<bool>> SetFlagsAsync(
        Mailbox mailbox, IReadOnlyCollection<string> providerMessageIds, bool? isUnread, bool? isStarred, CancellationToken ct = default)
    {
        if (FailWith is null)
            Writes.Add($"flags:{string.Join(',', providerMessageIds)}:{isUnread}:{isStarred}");
        return Answer(true);
    }

    public Task<Result<bool>> ChangeLabelsAsync(
        Mailbox mailbox, IReadOnlyCollection<string> providerMessageIds, IReadOnlyCollection<string> add, IReadOnlyCollection<string> remove,
        CancellationToken ct = default)
    {
        if (FailWith is null)
            Writes.Add($"labels:{string.Join(',', providerMessageIds)}:+{string.Join(',', add)}:-{string.Join(',', remove)}");
        return Answer(true);
    }

    public Task<Result<IReadOnlyDictionary<string, string?>>> MoveAsync(
        Mailbox mailbox, IReadOnlyCollection<string> providerMessageIds, string targetProviderLabelId, CancellationToken ct = default)
    {
        if (FailWith is null)
            Writes.Add($"move:{string.Join(',', providerMessageIds)}:{targetProviderLabelId}");
        return Answer<IReadOnlyDictionary<string, string?>>(providerMessageIds.ToDictionary(id => id, id => MovedId(id)));
    }

    public Task<Result<IReadOnlyDictionary<string, string?>>> TrashAsync(
        Mailbox mailbox, IReadOnlyCollection<string> providerMessageIds, CancellationToken ct = default)
    {
        if (FailWith is null)
            Writes.Add($"trash:{string.Join(',', providerMessageIds)}");
        return Answer<IReadOnlyDictionary<string, string?>>(providerMessageIds.ToDictionary(id => id, id => MovedId(id)));
    }

    public Task<Result<SentMessage>> SendAsync(Mailbox mailbox, byte[] rawMessage, string? threadKey, CancellationToken ct = default)
    {
        if (FailWith is null)
            Sent.Add((rawMessage, threadKey));
        return Answer(new SentMessage(CopySaved));
    }

    private Task<Result<T>> Answer<T>(T value)
    {
        if (ThrowWith is { } exception)
            throw exception;

        return Task.FromResult(FailWith is { } error ? Result<T>.Failure(error) : Result<T>.Success(value));
    }
}
