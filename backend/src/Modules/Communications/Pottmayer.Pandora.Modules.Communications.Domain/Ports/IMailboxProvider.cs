using Pottmayer.Pandora.Modules.Communications.Domain.Entities;
using Pottmayer.Pandora.Modules.Communications.Domain.Errors;
using Pottmayer.Pandora.Modules.Communications.Domain.ValueObjects;
using Pottmayer.Tars.Core.Primitives.Outcomes;

namespace Pottmayer.Pandora.Modules.Communications.Domain.Ports;

/// <summary>
/// One mail provider (Gmail API, IMAP). Registered as a keyed service under each
/// <see cref="MailProvider"/> value it serves (the IMAP adapter under <c>titan</c> and <c>imap</c>).
/// </summary>
/// <remarks>
/// <para>The cursors are opaque: only the adapter that wrote one reads it (a Gmail <c>historyId</c>, the
/// IMAP per-folder UIDVALIDITY/UIDNEXT/MODSEQ). The sync engine stores them on the mailbox as they come.</para>
/// <para>Every call can fail with <see cref="MailErrors.MailboxAuthFailed"/>,
/// <see cref="MailErrors.MissingScopes"/> or <see cref="MailErrors.ProviderFailed"/>;
/// <see cref="FetchChangesAsync"/> also with <see cref="MailErrors.SyncCursorExpired"/>.</para>
/// </remarks>
public interface IMailboxProvider
{
    MailboxCapabilities Capabilities { get; }

    Task<Result<bool>> TestConnectionAsync(Mailbox mailbox, CancellationToken ct = default);

    Task<Result<IReadOnlyList<RemoteLabel>>> ListLabelsAsync(Mailbox mailbox, CancellationToken ct = default);

    /// <summary>
    /// Takes the incremental cursor <b>now</b>, before the first backfill batch, so what arrives during the
    /// backfill is seen as new by the next <see cref="FetchChangesAsync"/>.
    /// </summary>
    Task<Result<BackfillStart>> StartBackfillAsync(Mailbox mailbox, CancellationToken ct = default);

    /// <summary>The next batch below the backfill cursor, newest first.</summary>
    Task<Result<BackfillBatch>> FetchBackfillBatchAsync(
        Mailbox mailbox, string backfillCursor, int batchSize, CancellationToken ct = default);

    Task<Result<RemoteChanges>> FetchChangesAsync(Mailbox mailbox, string syncCursor, CancellationToken ct = default);

    /// <summary>
    /// The whole RFC 822 message, without marking it as read. ponytail: whole message in memory, and an
    /// attachment download fetches it again; per-part fetch (Gmail <c>attachments.get</c>, IMAP
    /// <c>BODY[part]</c>) if large messages hurt.
    /// </summary>
    Task<Result<byte[]>> GetRawMessageAsync(Mailbox mailbox, string providerMessageId, CancellationToken ct = default);

    // Writes. Nothing here deletes mail for good: "trash" is the provider's trash, and a folder with messages is
    // refused (LabelNotEmpty), since deleting an IMAP folder deletes what is inside it.

    Task<Result<RemoteLabel>> CreateLabelAsync(Mailbox mailbox, string name, CancellationToken ct = default);

    Task<Result<bool>> RenameLabelAsync(Mailbox mailbox, string providerLabelId, string newName, CancellationToken ct = default);

    Task<Result<bool>> DeleteLabelAsync(Mailbox mailbox, string providerLabelId, CancellationToken ct = default);

    /// <summary>A null flag stays as it is.</summary>
    Task<Result<bool>> SetFlagsAsync(
        Mailbox mailbox, IReadOnlyCollection<string> providerMessageIds, bool? isUnread, bool? isStarred, CancellationToken ct = default);

    /// <summary>Only where <see cref="MailboxCapabilities.MultipleLabels"/>; otherwise <c>ActionNotSupported</c>.</summary>
    Task<Result<bool>> ChangeLabelsAsync(
        Mailbox mailbox, IReadOnlyCollection<string> providerMessageIds, IReadOnlyCollection<string> add, IReadOnlyCollection<string> remove,
        CancellationToken ct = default);

    /// <summary>
    /// The message's id after the move, by its id before it. Null when the provider does not say: the next
    /// <see cref="FetchChangesAsync"/> reports it, and the Message-ID finds its row.
    /// </summary>
    Task<Result<IReadOnlyDictionary<string, string?>>> MoveAsync(
        Mailbox mailbox, IReadOnlyCollection<string> providerMessageIds, string targetProviderLabelId, CancellationToken ct = default);

    /// <summary>To the provider's trash, where it can be restored; the ids as in <see cref="MoveAsync"/>.</summary>
    Task<Result<IReadOnlyDictionary<string, string?>>> TrashAsync(
        Mailbox mailbox, IReadOnlyCollection<string> providerMessageIds, CancellationToken ct = default);

    /// <summary>
    /// Sends a whole RFC 822 message and sees that a sent copy exists. This cannot be taken back, so it is never
    /// repeated when the mail may have gone: that is <see cref="MailErrors.SendUncertain"/>.
    /// </summary>
    /// <param name="threadKey">The conversation of a reply, where the provider has them.</param>
    Task<Result<SentMessage>> SendAsync(Mailbox mailbox, byte[] rawMessage, string? threadKey, CancellationToken ct = default);
}

/// <param name="CopySaved">False when the mail went but no sent copy could be saved. The send still succeeded.</param>
public sealed record SentMessage(bool CopySaved);

/// <summary>What the screens may offer for a provider.</summary>
/// <param name="MultipleLabels">A message carries several labels (Gmail); otherwise a label is a folder.</param>
/// <param name="ServerFilters">The provider runs filters itself (Gmail, M4).</param>
/// <param name="NativeThreads">The provider groups conversations (Gmail <c>threadId</c>).</param>
public sealed record MailboxCapabilities(bool MultipleLabels, bool ServerFilters, bool NativeThreads);

public sealed record RemoteLabel(
    string ProviderLabelId, string Name, LabelKind Kind, LabelRole? Role, bool IsExclusive);

/// <summary>A message's full current state; <see cref="ProviderLabelIds"/> is its whole label set.</summary>
public sealed record RemoteMessage(
    string ProviderMessageId,
    string ThreadKey,
    string? Rfc822MessageId,
    MailAddress? From,
    IReadOnlyList<MailAddress> To,
    IReadOnlyList<MailAddress> Cc,
    string Subject,
    string Snippet,
    DateTimeOffset SentAt,
    bool IsUnread,
    bool IsStarred,
    bool HasAttachments,
    long SizeBytes,
    IReadOnlyList<string> ProviderLabelIds);

/// <param name="SyncCursor">Where the incremental sync starts.</param>
/// <param name="BackfillCursor">Where the first batch starts; null when the mailbox is empty.</param>
/// <param name="TotalEstimate">The provider's message count, for the progress bar.</param>
public sealed record BackfillStart(string SyncCursor, string? BackfillCursor, int? TotalEstimate);

/// <param name="NextCursor">Null when the backfill is done.</param>
public sealed record BackfillBatch(IReadOnlyList<RemoteMessage> Messages, string? NextCursor);

/// <param name="Upserts">New messages and messages whose labels or flags changed, in full.</param>
/// <param name="RemovedIds">Provider message ids gone from the mailbox.</param>
/// <param name="Snapshots">
/// For a provider that cannot report removals (IMAP without QRESYNC): every message a label has now.
/// A local message under that label and not in the set loses it, and a message left with no label is
/// removed.
/// </param>
public sealed record RemoteChanges(
    IReadOnlyList<RemoteMessage> Upserts,
    IReadOnlyList<string> RemovedIds,
    IReadOnlyList<RemoteLabelSnapshot> Snapshots,
    string SyncCursor);

public sealed record RemoteLabelSnapshot(string ProviderLabelId, IReadOnlyCollection<string> ProviderMessageIds);
