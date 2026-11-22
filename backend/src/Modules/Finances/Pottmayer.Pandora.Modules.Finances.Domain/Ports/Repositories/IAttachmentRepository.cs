using Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;
using Pottmayer.Tars.Data.Abstractions.Repositories;

namespace Pottmayer.Pandora.Modules.Finances.Domain.Ports.Repositories;

public interface IAttachmentRepository : IStandardRepository<Attachment, Guid>
{
    /// <summary>One attachment owned by the user, or <c>null</c> (used for the 404-on-foreign-resource rule).</summary>
    Task<Attachment?> FindByIdForUserAsync(Guid id, Guid userId, CancellationToken ct = default);

    /// <summary>
    /// The user's attachments on one owner (pass one id), or the queued ones (pass none), oldest first.
    /// </summary>
    Task<IReadOnlyList<Attachment>> GetByOwnerAsync(
        Guid userId, Guid? transactionId, Guid? pendingTransactionId, Guid? cardStatementId, CancellationToken ct = default);

    /// <summary>How many attachments each of these transactions has; ids without any are absent.</summary>
    Task<IReadOnlyDictionary<Guid, int>> CountByTransactionsAsync(IReadOnlyCollection<Guid> transactionIds, CancellationToken ct = default);

    /// <summary>How many attachments each of these pending transactions has; ids without any are absent.</summary>
    Task<IReadOnlyDictionary<Guid, int>> CountByPendingTransactionsAsync(IReadOnlyCollection<Guid> pendingTransactionIds, CancellationToken ct = default);
}
