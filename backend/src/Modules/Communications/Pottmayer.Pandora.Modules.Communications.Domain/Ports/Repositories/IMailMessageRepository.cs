using Pottmayer.Pandora.Modules.Communications.Domain.Entities;
using Pottmayer.Tars.Data.Abstractions.Repositories;

namespace Pottmayer.Pandora.Modules.Communications.Domain.Ports.Repositories;

/// <summary>Every read that returns messages loads their labels with them.</summary>
public interface IMailMessageRepository : IStandardRepository<MailMessage, Guid>
{
    Task<MailMessage?> FindByProviderIdAsync(Guid mailboxId, string providerMessageId, CancellationToken ct = default);

    Task<IReadOnlyList<MailMessage>> ListByProviderIdsAsync(
        Guid mailboxId, IReadOnlyCollection<string> providerMessageIds, CancellationToken ct = default);

    /// <summary>Id and provider id of every message under a label, untracked — for diffing against a snapshot.</summary>
    Task<IReadOnlyList<(Guid Id, string ProviderMessageId)>> ListKeysUnderLabelAsync(Guid labelId, CancellationToken ct = default);

    Task<IReadOnlyList<MailMessage>> ListByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default);

    Task<bool> ExistsWithRfc822IdAsync(Guid mailboxId, string rfc822MessageId, CancellationToken ct = default);

    Task<int> DeleteByProviderIdsAsync(Guid mailboxId, IReadOnlyCollection<string> providerMessageIds, CancellationToken ct = default);

    /// <summary>Newest first, keyset on (<c>sent_at</c>, <c>id</c>), untracked.</summary>
    Task<IReadOnlyList<MailMessage>> ListPageAsync(MailMessageQuery query, CancellationToken ct = default);

    Task<IReadOnlyDictionary<Guid, int>> CountByMailboxAsync(IReadOnlyCollection<Guid> mailboxIds, CancellationToken ct = default);

    /// <summary>Per label of the mailbox: every message under it and the unread ones.</summary>
    Task<IReadOnlyDictionary<Guid, (int Total, int Unread)>> CountByLabelAsync(Guid mailboxId, CancellationToken ct = default);

    /// <summary>Every message of the mailbox (labels and cached bodies cascade).</summary>
    Task<int> DeleteByMailboxAsync(Guid mailboxId, CancellationToken ct = default);
}

/// <summary>
/// A page of the message list. <see cref="Search"/> matches subject, sender and snippet;
/// <see cref="Before"/> is the last row of the previous page.
/// </summary>
public sealed record MailMessageQuery(
    IReadOnlyCollection<Guid> MailboxIds,
    Guid? LabelId,
    bool? Unread,
    string? Search,
    string? ThreadKey,
    (DateTimeOffset SentAt, Guid Id)? Before,
    int Limit);
