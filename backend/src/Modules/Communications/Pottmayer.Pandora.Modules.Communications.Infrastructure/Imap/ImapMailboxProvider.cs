using MailKit;
using MailKit.Net.Imap;
using MailKit.Net.Smtp;
using MailKit.Search;
using MailKit.Security;
using Pottmayer.Pandora.Modules.Communications.Domain.Entities;
using Pottmayer.Pandora.Modules.Communications.Domain.Errors;
using Pottmayer.Pandora.Modules.Communications.Domain.Ports;
using Pottmayer.Pandora.Modules.Communications.Domain.ValueObjects;
using Pottmayer.Pandora.Modules.Integrations.Abstractions.Ports;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using static Pottmayer.Pandora.Modules.Communications.Infrastructure.Imap.ImapCursors;

namespace Pottmayer.Pandora.Modules.Communications.Infrastructure.Imap;

/// <summary>
/// Titan and any IMAP server, through MailKit. One connection per call, logged in with the mailbox's
/// password account. A folder is a label (<c>is_exclusive</c>), identified by its full path.
/// </summary>
/// <remarks>
/// Folders in scope: every selectable one of the personal namespace, INBOX first. Subscriptions are ignored: a rule
/// that depends on them makes the whole mirror flip when any client subscribes the first folder.
/// New messages are the UIDs at or above the folder's UIDNEXT; flag changes come from CONDSTORE when the
/// server has it, otherwise from re-reading the last <see cref="FlagWindow"/> UIDs; removals from a
/// snapshot of the folder's UIDs (no QRESYNC needed).
/// </remarks>
internal sealed class ImapMailboxProvider(IExternalCredentialProvider credentials) : IMailboxProvider
{
    // ponytail: without CONDSTORE a flag change on an older message is missed; a full flag diff per
    // cycle if that matters on a real server.
    internal const int FlagWindow = 200;

    /// <summary>How long a server gets to save its own copy of a sent message.</summary>
    private static readonly TimeSpan SentCopyWait = TimeSpan.FromSeconds(3);

    /// <summary>For a server with no SPECIAL-USE, where no folder has the role.</summary>
    private static readonly string[] SentFolderNames = ["Sent", "Sent Items", "Sent Messages", "Enviados"];

    private const MessageSummaryItems SummaryItems =
        MessageSummaryItems.UniqueId | MessageSummaryItems.Envelope | MessageSummaryItems.Flags |
        MessageSummaryItems.BodyStructure | MessageSummaryItems.Size | MessageSummaryItems.References |
        MessageSummaryItems.InternalDate;

    public MailboxCapabilities Capabilities { get; } = new(MultipleLabels: false, ServerFilters: false, NativeThreads: false);

    public Task<Result<bool>> TestConnectionAsync(Mailbox mailbox, CancellationToken ct = default) =>
        RunAsync(mailbox, (_, _) => Task.FromResult(Result<bool>.Success(true)), ct);

    public Task<Result<IReadOnlyList<RemoteLabel>>> ListLabelsAsync(Mailbox mailbox, CancellationToken ct = default) =>
        RunAsync<IReadOnlyList<RemoteLabel>>(mailbox, async (client, token) =>
        {
            var folders = await FoldersInScopeAsync(client, token);
            return Result<IReadOnlyList<RemoteLabel>>.Success(folders.Select(ToLabel).ToList());
        }, ct);

    public Task<Result<BackfillStart>> StartBackfillAsync(Mailbox mailbox, CancellationToken ct = default) =>
        RunAsync(mailbox, async (client, token) =>
        {
            var states = new Dictionary<string, FolderState>();
            var pending = new Dictionary<string, BackfillState>();
            var total = 0;

            foreach (var folder in await FoldersInScopeAsync(client, token))
            {
                await folder.OpenAsync(FolderAccess.ReadOnly, token);
                var state = await ReadStateAsync(client, folder, token);
                states[folder.FullName] = state;
                total += folder.Count;
                if (folder.Count > 0)
                    pending[folder.FullName] = new BackfillState(state.UidValidity, state.UidNext);
                await folder.CloseAsync(false, token);
            }

            return Result<BackfillStart>.Success(
                new BackfillStart(Write(states), pending.Count > 0 ? Write(pending) : null, total));
        }, ct);

    /// <summary>One folder per batch, in the order the backfill started them (INBOX first).</summary>
    public Task<Result<BackfillBatch>> FetchBackfillBatchAsync(
        Mailbox mailbox, string backfillCursor, int batchSize, CancellationToken ct = default) =>
        RunAsync(mailbox, async (client, token) =>
        {
            var pending = Read<BackfillState>(backfillCursor);
            var messages = new List<RemoteMessage>();

            if (pending.Count > 0)
            {
                var (path, state) = pending.First();
                var folder = await TryOpenAsync(client, path, token);

                // Gone or renumbered: the incremental sync deals with it, the backfill moves on.
                if (folder is null || folder.UidValidity != state.UidValidity || state.Below <= 1)
                {
                    pending.Remove(path);
                }
                else
                {
                    var range = new UniqueIdRange(UniqueId.MinValue, new UniqueId(state.Below - 1));
                    var uids = (await folder.SearchAsync(SearchQuery.Uids(range), token))
                        .OrderByDescending(u => u.Id)
                        .Take(batchSize)
                        .ToList();

                    messages.AddRange(await FetchAsync(folder, uids, token));

                    if (uids.Count < batchSize || uids[^1].Id <= 1)
                        pending.Remove(path);
                    else
                        pending[path] = state with { Below = uids[^1].Id };
                }

                if (folder is not null)
                    await folder.CloseAsync(false, token);
            }

            return Result<BackfillBatch>.Success(new BackfillBatch(messages, pending.Count > 0 ? Write(pending) : null));
        }, ct);

    public Task<Result<RemoteChanges>> FetchChangesAsync(Mailbox mailbox, string syncCursor, CancellationToken ct = default) =>
        RunAsync(mailbox, async (client, token) =>
        {
            var known = Read<FolderState>(syncCursor);
            var next = new Dictionary<string, FolderState>();
            var upserts = new List<RemoteMessage>();
            var snapshots = new List<RemoteLabelSnapshot>();

            foreach (var folder in await FoldersInScopeAsync(client, token))
            {
                await folder.OpenAsync(FolderAccess.ReadOnly, token);
                var path = folder.FullName;
                var all = await folder.SearchAsync(SearchQuery.All, token);

                known.TryGetValue(path, out var before);
                if (before is not null && before.UidValidity != folder.UidValidity)
                    return Result<RemoteChanges>.Failure(MailErrors.SyncCursorExpired);

                // A folder the cursor does not know yet (created since): all of it is new.
                var boundary = before?.UidNext ?? 1;
                var fresh = all.Where(u => u.Id >= boundary).ToList();
                var changed = await ChangedSinceAsync(client, folder, before, all, boundary, token);

                upserts.AddRange(await FetchAsync(folder, [.. fresh, .. changed], token));
                snapshots.Add(new RemoteLabelSnapshot(path, all.Select(u => MessageId(folder.UidValidity, u.Id, path)).ToList()));

                var state = await ReadStateAsync(client, folder, token);
                next[path] = state with { UidNext = Math.Max(state.UidNext, boundary) };
                await folder.CloseAsync(false, token);
            }

            // Gone (deleted, or renamed): its messages are gone from this folder.
            foreach (var path in known.Keys.Where(p => !next.ContainsKey(p)))
                snapshots.Add(new RemoteLabelSnapshot(path, []));

            return Result<RemoteChanges>.Success(new RemoteChanges(upserts, [], snapshots, Write(next)));
        }, ct);

    public Task<Result<byte[]>> GetRawMessageAsync(Mailbox mailbox, string providerMessageId, CancellationToken ct = default) =>
        RunAsync(mailbox, async (client, token) =>
        {
            if (!TryParseMessageId(providerMessageId, out var uidValidity, out var uid, out var path))
                return Result<byte[]>.Failure(MailErrors.ProviderFailed($"Not an IMAP message id: '{providerMessageId}'."));

            var folder = await TryOpenAsync(client, path, token);
            if (folder is null || folder.UidValidity != uidValidity)
                return Result<byte[]>.Failure(MailErrors.SyncCursorExpired);

            // GetMessage fetches BODY.PEEK[]: reading in Pandora does not mark the message as read.
            using var message = await folder.GetMessageAsync(new UniqueId(uid), token);
            using var buffer = new MemoryStream();
            await message.WriteToAsync(buffer, token);
            await folder.CloseAsync(false, token);
            return Result<byte[]>.Success(buffer.ToArray());
        }, ct);

    public Task<Result<RemoteLabel>> CreateLabelAsync(Mailbox mailbox, string name, CancellationToken ct = default) =>
        RunAsync(mailbox, async (client, token) =>
        {
            if (client.PersonalNamespaces.Count == 0)
                return Result<RemoteLabel>.Failure(MailErrors.ActionNotSupported);

            var root = client.GetFolder(client.PersonalNamespaces[0]);
            if (name.Contains(root.DirectorySeparator))
                return Result<RemoteLabel>.Failure(MailErrors.LabelNameInvalid);

            var folder = await root.CreateAsync(name, isMessageFolder: true, token);
            return Result<RemoteLabel>.Success(ToLabel(folder));
        }, ct);

    public Task<Result<bool>> RenameLabelAsync(Mailbox mailbox, string providerLabelId, string newName, CancellationToken ct = default) =>
        RunAsync(mailbox, async (client, token) =>
        {
            var folder = await client.GetFolderAsync(providerLabelId, token);
            if (IsSystem(folder))
                return Result<bool>.Failure(MailErrors.SystemLabel);
            if (newName.Contains(folder.DirectorySeparator))
                return Result<bool>.Failure(MailErrors.LabelNameInvalid);

            await folder.RenameAsync(folder.ParentFolder, newName, token);
            return Result<bool>.Success(true);
        }, ct);

    public Task<Result<bool>> DeleteLabelAsync(Mailbox mailbox, string providerLabelId, CancellationToken ct = default) =>
        RunAsync(mailbox, async (client, token) =>
        {
            var folder = await client.GetFolderAsync(providerLabelId, token);
            if (IsSystem(folder))
                return Result<bool>.Failure(MailErrors.SystemLabel);

            // Asked of the server, not of the mirror: the mirror can be a cycle behind.
            await folder.StatusAsync(StatusItems.Count, token);
            if (folder.Count > 0 || (await folder.GetSubfoldersAsync(false, token)).Count > 0)
                return Result<bool>.Failure(MailErrors.LabelNotEmpty);

            await folder.DeleteAsync(token);
            return Result<bool>.Success(true);
        }, ct);

    public Task<Result<bool>> SetFlagsAsync(
        Mailbox mailbox, IReadOnlyCollection<string> providerMessageIds, bool? isUnread, bool? isStarred, CancellationToken ct = default) =>
        RunAsync(mailbox, async (client, token) =>
        {
            foreach (var group in GroupByFolder(providerMessageIds))
            {
                var folder = await TryOpenAsync(client, group.Path, token, FolderAccess.ReadWrite);
                if (folder is null || folder.UidValidity != group.UidValidity)
                    return Result<bool>.Failure(MailErrors.SyncCursorExpired);

                if (isUnread is { } unread)
                    await (unread
                        ? folder.RemoveFlagsAsync(group.Uids, MessageFlags.Seen, silent: true, token)
                        : folder.AddFlagsAsync(group.Uids, MessageFlags.Seen, silent: true, token));
                if (isStarred is { } starred)
                    await (starred
                        ? folder.AddFlagsAsync(group.Uids, MessageFlags.Flagged, silent: true, token)
                        : folder.RemoveFlagsAsync(group.Uids, MessageFlags.Flagged, silent: true, token));

                await folder.CloseAsync(false, token);
            }

            return Result<bool>.Success(true);
        }, ct);

    public Task<Result<bool>> ChangeLabelsAsync(
        Mailbox mailbox, IReadOnlyCollection<string> providerMessageIds, IReadOnlyCollection<string> add, IReadOnlyCollection<string> remove,
        CancellationToken ct = default) =>
        Task.FromResult(Result<bool>.Failure(MailErrors.ActionNotSupported));

    public Task<Result<IReadOnlyDictionary<string, string?>>> MoveAsync(
        Mailbox mailbox, IReadOnlyCollection<string> providerMessageIds, string targetProviderLabelId, CancellationToken ct = default) =>
        RunAsync(mailbox, async (client, token) =>
        {
            var target = IsInboxPath(targetProviderLabelId) ? client.Inbox : await client.GetFolderAsync(targetProviderLabelId, token);
            return await MoveToAsync(client, providerMessageIds, target, token);
        }, ct);

    public Task<Result<IReadOnlyDictionary<string, string?>>> TrashAsync(
        Mailbox mailbox, IReadOnlyCollection<string> providerMessageIds, CancellationToken ct = default) =>
        RunAsync(mailbox, async (client, token) =>
        {
            var trash = (await FoldersInScopeAsync(client, token)).FirstOrDefault(f => ToLabel(f).Role == LabelRole.Trash);
            return trash is null
                ? Result<IReadOnlyDictionary<string, string?>>.Failure(MailErrors.NoTrashFolder)
                : await MoveToAsync(client, providerMessageIds, trash, token);
        }, ct);

    /// <summary>
    /// SMTP, then the sent copy. Nothing is repeated here: only an answer from the server says the mail did not go, and
    /// anything else after connecting is <see cref="MailErrors.SendUncertain"/>. A copy that fails does not fail the send.
    /// </summary>
    public async Task<Result<SentMessage>> SendAsync(Mailbox mailbox, byte[] rawMessage, string? threadKey, CancellationToken ct = default)
    {
        var server = mailbox.Server
                     ?? throw new InvalidOperationException($"Mailbox {mailbox.Id} ({mailbox.Provider}) has no SMTP server.");

        var credential = await credentials.GetPasswordAsync(mailbox.UserId, mailbox.ExternalAccountId, ct);
        if (!credential.IsSuccess)
            return Result<SentMessage>.Failure(MailErrors.MailboxAuthFailed(string.Join("; ", credential.Errors.Select(e => e.Message))));

        using var stream = new MemoryStream(rawMessage);
        var message = await MimeKit.MimeMessage.LoadAsync(stream, ct);

        using var smtp = new SmtpClient();
        try
        {
            await smtp.ConnectAsync(server.SmtpHost, server.SmtpPort, SecureSocketOptions.Auto, ct);
            if (smtp.Capabilities.HasFlag(SmtpCapabilities.Authentication))
                await smtp.AuthenticateAsync(credential.Value.Username, credential.Value.Password, ct);
        }
        catch (AuthenticationException ex)
        {
            return Result<SentMessage>.Failure(MailErrors.MailboxAuthFailed(ex.Message));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Result<SentMessage>.Failure(MailErrors.ProviderFailed(ex.Message));
        }

        try
        {
            // MailKit keeps Bcc on the envelope and off the wire.
            await smtp.SendAsync(message, ct);
        }
        catch (SmtpCommandException ex)
        {
            return Result<SentMessage>.Failure(MailErrors.ProviderFailed(ex.Message));
        }
        catch (Exception)
        {
            return Result<SentMessage>.Failure(MailErrors.SendUncertain);
        }

        try
        {
            await smtp.DisconnectAsync(true, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The server has the message; only the goodbye was lost.
        }

        var copy = await RunAsync(mailbox, (client, token) => SaveSentCopyAsync(client, message, token), ct);
        return Result<SentMessage>.Success(new SentMessage(copy.IsSuccess && copy.Value));
    }

    /// <summary>
    /// Many servers save the copy themselves, some a moment later, and nothing says which: look for it, and add it only
    /// if it is not there. ponytail: a server slower than <see cref="SentCopyWait"/> ends up with two copies.
    /// </summary>
    private static async Task<Result<bool>> SaveSentCopyAsync(ImapClient client, MimeKit.MimeMessage message, CancellationToken ct)
    {
        var folders = await FoldersInScopeAsync(client, ct);
        var sent = folders.FirstOrDefault(f => ToLabel(f).Role == LabelRole.Sent)
                   ?? folders.FirstOrDefault(f => SentFolderNames.Contains(f.Name, StringComparer.OrdinalIgnoreCase));
        if (sent is null)
            return Result<bool>.Success(false);

        await sent.OpenAsync(FolderAccess.ReadWrite, ct);
        var query = SearchQuery.HeaderContains("Message-ID", message.MessageId);
        if ((await sent.SearchAsync(query, ct)).Count == 0)
        {
            await Task.Delay(SentCopyWait, ct);
            if ((await sent.SearchAsync(query, ct)).Count == 0)
                await sent.AppendAsync(new AppendRequest(message, MessageFlags.Seen), ct);
        }

        await sent.CloseAsync(false, ct);
        return Result<bool>.Success(true);
    }

    /// <summary>A UID is per folder, so a move gets a new one; without UIDPLUS the server does not say which.</summary>
    private static async Task<Result<IReadOnlyDictionary<string, string?>>> MoveToAsync(
        ImapClient client, IReadOnlyCollection<string> providerMessageIds, IMailFolder target, CancellationToken ct)
    {
        var moved = new Dictionary<string, string?>();

        foreach (var group in GroupByFolder(providerMessageIds))
        {
            if (string.Equals(group.Path, target.FullName, StringComparison.Ordinal))
            {
                foreach (var uid in group.Uids)
                    moved[MessageId(group.UidValidity, uid.Id, group.Path)] = MessageId(group.UidValidity, uid.Id, group.Path);
                continue;
            }

            var source = await TryOpenAsync(client, group.Path, ct, FolderAccess.ReadWrite);
            if (source is null || source.UidValidity != group.UidValidity)
                return Result<IReadOnlyDictionary<string, string?>>.Failure(MailErrors.SyncCursorExpired);

            var map = await source.MoveToAsync(group.Uids, target, ct);
            foreach (var uid in group.Uids)
                moved[MessageId(group.UidValidity, uid.Id, group.Path)] =
                    map.TryGetValue(uid, out var next) ? MessageId(next.Validity, next.Id, target.FullName) : null;

            await source.CloseAsync(false, ct);
        }

        return Result<IReadOnlyDictionary<string, string?>>.Success(moved);
    }

    private sealed record FolderGroup(string Path, uint UidValidity, IList<UniqueId> Uids);

    /// <summary>An id that is not one of this adapter's is a bug in the caller: it fails the whole action.</summary>
    private static List<FolderGroup> GroupByFolder(IReadOnlyCollection<string> providerMessageIds)
    {
        var groups = new Dictionary<(string Path, uint UidValidity), List<UniqueId>>();
        foreach (var id in providerMessageIds)
        {
            if (!TryParseMessageId(id, out var uidValidity, out var uid, out var path))
                throw new ArgumentException($"Not an IMAP message id: '{id}'.", nameof(providerMessageIds));

            if (!groups.TryGetValue((path, uidValidity), out var uids))
                groups[(path, uidValidity)] = uids = [];
            uids.Add(new UniqueId(uid));
        }

        return groups.Select(g => new FolderGroup(g.Key.Path, g.Key.UidValidity, g.Value)).ToList();
    }

    private static bool IsSystem(IMailFolder folder) => IsInbox(folder) || ToLabel(folder).Role is not null;

    /// <summary>Connects and logs in, runs <paramref name="work"/>, and maps what the server says to the port's errors.</summary>
    private async Task<Result<T>> RunAsync<T>(
        Mailbox mailbox, Func<ImapClient, CancellationToken, Task<Result<T>>> work, CancellationToken ct)
    {
        var server = mailbox.Server
                     ?? throw new InvalidOperationException($"Mailbox {mailbox.Id} ({mailbox.Provider}) has no IMAP server.");

        var credential = await credentials.GetPasswordAsync(mailbox.UserId, mailbox.ExternalAccountId, ct);
        if (!credential.IsSuccess)
            return Result<T>.Failure(MailErrors.MailboxAuthFailed(string.Join("; ", credential.Errors.Select(e => e.Message))));

        using var client = new ImapClient();
        try
        {
            await client.ConnectAsync(server.ImapHost, server.ImapPort, SecureSocketOptions.Auto, ct);
            await client.AuthenticateAsync(credential.Value.Username, credential.Value.Password, ct);
            var result = await work(client, ct);
            await client.DisconnectAsync(true, ct);
            return result;
        }
        catch (AuthenticationException ex)
        {
            return Result<T>.Failure(MailErrors.MailboxAuthFailed(ex.Message));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Result<T>.Failure(MailErrors.ProviderFailed(ex.Message));
        }
    }

    private static async Task<IReadOnlyList<IMailFolder>> FoldersInScopeAsync(ImapClient client, CancellationToken ct)
    {
        if (client.PersonalNamespaces.Count == 0)
            return [client.Inbox];

        var folders = await client.GetFoldersAsync(client.PersonalNamespaces[0], StatusItems.None, subscribedOnly: false, ct);

        return
        [
            client.Inbox,
            .. folders.Where(f => !IsInbox(f) &&
                                  !f.Attributes.HasFlag(FolderAttributes.NoSelect) &&
                                  !f.Attributes.HasFlag(FolderAttributes.NonExistent))
        ];
    }

    private static async Task<IMailFolder?> TryOpenAsync(
        ImapClient client, string path, CancellationToken ct, FolderAccess access = FolderAccess.ReadOnly)
    {
        try
        {
            var folder = IsInboxPath(path) ? client.Inbox : await client.GetFolderAsync(path, ct);
            await folder.OpenAsync(access, ct);
            return folder;
        }
        catch (FolderNotFoundException)
        {
            return null;
        }
    }

    private static bool IsInbox(IMailFolder folder) => IsInboxPath(folder.FullName);

    private static bool IsInboxPath(string path) => string.Equals(path, "INBOX", StringComparison.OrdinalIgnoreCase);

    /// <summary>The open folder's cursor. A server that does not send UIDNEXT gets the highest UID + 1.</summary>
    private static async Task<FolderState> ReadStateAsync(ImapClient client, IMailFolder folder, CancellationToken ct)
    {
        var uidNext = folder.UidNext?.Id;
        if (uidNext is null)
        {
            var all = await folder.SearchAsync(SearchQuery.All, ct);
            uidNext = all.Count == 0 ? 1 : all.Max(u => u.Id) + 1;
        }

        var modSeq = client.Capabilities.HasFlag(ImapCapabilities.CondStore) ? folder.HighestModSeq : (ulong?)null;
        return new FolderState(folder.UidValidity, uidNext.Value, modSeq);
    }

    private static async Task<IReadOnlyList<UniqueId>> ChangedSinceAsync(
        ImapClient client, IMailFolder folder, FolderState? before, IList<UniqueId> all, uint boundary, CancellationToken ct)
    {
        if (before is null)
            return [];

        if (before.HighestModSeq is { } modSeq && client.Capabilities.HasFlag(ImapCapabilities.CondStore))
        {
            var changed = await folder.FetchAsync(UniqueIdRange.All, modSeq, MessageSummaryItems.UniqueId, ct);
            return changed.Select(s => s.UniqueId).Where(u => u.Id < boundary).ToList();
        }

        return all.Where(u => u.Id < boundary).OrderByDescending(u => u.Id).Take(FlagWindow).ToList();
    }

    private static async Task<IReadOnlyList<RemoteMessage>> FetchAsync(
        IMailFolder folder, IList<UniqueId> uids, CancellationToken ct)
    {
        if (uids.Count == 0)
            return [];

        var summaries = await folder.FetchAsync(uids, SummaryItems, ct);
        return summaries.OrderByDescending(s => s.UniqueId.Id).Select(s => ToMessage(folder, s)).ToList();
    }

    private static RemoteMessage ToMessage(IMailFolder folder, IMessageSummary summary)
    {
        var id = MessageId(folder.UidValidity, summary.UniqueId.Id, folder.FullName);
        var envelope = summary.Envelope;
        var from = envelope?.From.Mailboxes.FirstOrDefault();
        var flags = summary.Flags ?? MessageFlags.None;

        return new RemoteMessage(
            ProviderMessageId: id,
            ThreadKey: ThreadKey.From(envelope?.MessageId, summary.References, id),
            Rfc822MessageId: envelope?.MessageId,
            From: from is null ? null : new MailAddress(NullIfEmpty(from.Name), from.Address),
            To: Addresses(envelope?.To),
            Cc: Addresses(envelope?.Cc),
            Subject: envelope?.Subject ?? string.Empty,
            Snippet: string.Empty,
            SentAt: envelope?.Date ?? summary.InternalDate ?? DateTimeOffset.UnixEpoch,
            IsUnread: !flags.HasFlag(MessageFlags.Seen),
            IsStarred: flags.HasFlag(MessageFlags.Flagged),
            HasAttachments: summary.Attachments.Any(),
            SizeBytes: summary.Size ?? 0,
            ProviderLabelIds: [folder.FullName]);
    }

    private static List<MailAddress> Addresses(MimeKit.InternetAddressList? list) =>
        list?.Mailboxes.Select(m => new MailAddress(NullIfEmpty(m.Name), m.Address)).ToList() ?? [];

    private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    private static RemoteLabel ToLabel(IMailFolder folder)
    {
        var role = LabelRole.FromImapFolder(folder.FullName, SpecialUse(folder.Attributes));
        return new RemoteLabel(folder.FullName, folder.FullName, role is null ? LabelKind.User : LabelKind.System, role, IsExclusive: true);
    }

    /// <summary>MailKit parses the SPECIAL-USE attributes into flags; the domain reads them as RFC 6154 names.</summary>
    private static IEnumerable<string> SpecialUse(FolderAttributes attributes)
    {
        if (attributes.HasFlag(FolderAttributes.Sent)) yield return @"\Sent";
        if (attributes.HasFlag(FolderAttributes.Drafts)) yield return @"\Drafts";
        if (attributes.HasFlag(FolderAttributes.Trash)) yield return @"\Trash";
        if (attributes.HasFlag(FolderAttributes.Junk)) yield return @"\Junk";
        if (attributes.HasFlag(FolderAttributes.Archive)) yield return @"\Archive";
    }
}
