using Microsoft.EntityFrameworkCore;
using Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Finances.Domain.Ports.Repositories;
using Pottmayer.Tars.Data.Abstractions.DataContext;
using Pottmayer.Tars.Data.Relational.Repositories;

namespace Pottmayer.Pandora.Modules.Finances.Persistence.Repositories;

public sealed class AttachmentRepository(IDataContextAccessor accessor)
    : StandardRepository<Attachment, Guid>(accessor), IAttachmentRepository
{
    public Task<Attachment?> FindByIdForUserAsync(Guid id, Guid userId, CancellationToken ct = default)
        => Queryable().FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId, ct);

    public async Task<IReadOnlyList<Attachment>> GetByOwnerAsync(
        Guid userId, Guid? transactionId, Guid? pendingTransactionId, CancellationToken ct = default)
        => await Queryable()
            .Where(a => a.UserId == userId
                        && (transactionId == null || a.TransactionId == transactionId)
                        && (pendingTransactionId == null || a.PendingTransactionId == pendingTransactionId))
            .OrderBy(a => a.CreatedAt)
            .ToListAsync(ct);

    public async Task<IReadOnlyDictionary<Guid, int>> CountByTransactionsAsync(
        IReadOnlyCollection<Guid> transactionIds, CancellationToken ct = default)
    {
        if (transactionIds.Count == 0)
            return new Dictionary<Guid, int>();

        return await Queryable()
            .Where(a => a.TransactionId != null && transactionIds.Contains(a.TransactionId.Value))
            .GroupBy(a => a.TransactionId!.Value)
            .ToDictionaryAsync(g => g.Key, g => g.Count(), ct);
    }

    public async Task<IReadOnlyDictionary<Guid, int>> CountByPendingTransactionsAsync(
        IReadOnlyCollection<Guid> pendingTransactionIds, CancellationToken ct = default)
    {
        if (pendingTransactionIds.Count == 0)
            return new Dictionary<Guid, int>();

        return await Queryable()
            .Where(a => a.PendingTransactionId != null && pendingTransactionIds.Contains(a.PendingTransactionId.Value))
            .GroupBy(a => a.PendingTransactionId!.Value)
            .ToDictionaryAsync(g => g.Key, g => g.Count(), ct);
    }
}
