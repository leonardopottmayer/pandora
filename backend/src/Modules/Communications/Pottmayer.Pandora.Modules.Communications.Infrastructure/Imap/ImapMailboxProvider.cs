using MailKit;
using MailKit.Net.Imap;
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
/// Folders in scope: the subscribed ones, or every selectable one when none is subscribed; INBOX always.
/// New messages are the UIDs at or above the folder's UIDNEXT; flag changes come from CONDSTORE when the
/// server has it, otherwise from re-reading the last <see cref="FlagWindow"/> UIDs; removals from a
/// snapshot of the folder's UIDs (no QRESYNC needed).
/// </remarks>
internal sealed class ImapMailboxProvider(IExternalCredentialProvider credentials) : IMailboxProvider
{
    // ponytail: without CONDSTORE a flag change on an older message is missed; a full flag diff per
    // cycle if that matters on a real server.
    internal const int FlagWindow = 200;

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

                // A folder the cursor does not know yet (created or subscribed since): all of it is new.
                var boundary = before?.UidNext ?? 1;
                var fresh = all.Where(u => u.Id >= boundary).ToList();
                var changed = await ChangedSinceAsync(client, folder, before, all, boundary, token);

                upserts.AddRange(await FetchAsync(folder, [.. fresh, .. changed], token));
                snapshots.Add(new RemoteLabelSnapshot(path, all.Select(u => MessageId(folder.UidValidity, u.Id, path)).ToList()));

                var state = await ReadStateAsync(client, folder, token);
                next[path] = state with { UidNext = Math.Max(state.UidNext, boundary) };
                await folder.CloseAsync(false, token);
            }

            // Out of scope now (deleted, unsubscribed): its messages are gone from Pandora's view.
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

        var root = client.PersonalNamespaces[0];
        var subscribed = await client.GetFoldersAsync(root, StatusItems.None, subscribedOnly: true, ct);
        var folders = subscribed.Any(f => !IsInbox(f))
            ? subscribed
            : await client.GetFoldersAsync(root, StatusItems.None, subscribedOnly: false, ct);

        return
        [
            client.Inbox,
            .. folders.Where(f => !IsInbox(f) &&
                                  !f.Attributes.HasFlag(FolderAttributes.NoSelect) &&
                                  !f.Attributes.HasFlag(FolderAttributes.NonExistent))
        ];
    }

    private static async Task<IMailFolder?> TryOpenAsync(ImapClient client, string path, CancellationToken ct)
    {
        try
        {
            var folder = IsInboxPath(path) ? client.Inbox : await client.GetFolderAsync(path, ct);
            await folder.OpenAsync(FolderAccess.ReadOnly, ct);
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
