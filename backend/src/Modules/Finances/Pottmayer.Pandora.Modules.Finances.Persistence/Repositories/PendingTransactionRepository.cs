using Microsoft.EntityFrameworkCore;
using Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Finances.Domain.Ports.Repositories;
using Pottmayer.Tars.Data.Abstractions.DataContext;
using Pottmayer.Tars.Data.Relational.Repositories;

namespace Pottmayer.Pandora.Modules.Finances.Persistence.Repositories;

public sealed class PendingTransactionRepository(IDataContextAccessor accessor)
    : StandardRepository<PendingTransaction, Guid>(accessor), IPendingTransactionRepository
{
    public Task<PendingTransaction?> FindByIdForUserAsync(Guid id, Guid userId, CancellationToken ct = default)
        => Queryable().FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId, ct);

    public async Task<IReadOnlyList<PendingTransaction>> QueryAsync(
        Guid userId, PendingTransactionFilter filter, CancellationToken ct = default)
    {
        var query = Queryable().Where(p => p.UserId == userId && p.Status == "pending");

        if (filter.Source is not null)
            query = query.Where(p => p.Source == filter.Source);
        if (filter.AccountId is not null)
            query = query.Where(p => p.AccountId == filter.AccountId);
        if (filter.CardId is not null)
            query = query.Where(p => p.CardId == filter.CardId);
        if (filter.From is not null)
            query = query.Where(p => p.OccurredOn >= filter.From);
        if (filter.To is not null)
            query = query.Where(p => p.OccurredOn <= filter.To);

        return await query
            .OrderBy(p => p.OccurredOn)
            .ThenBy(p => p.Id)
            .Skip(filter.Skip)
            .Take(filter.Take)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<PendingTransaction>> GetPendingByIdsForUserAsync(
        Guid userId, IReadOnlyCollection<Guid> ids, CancellationToken ct = default)
        => await Queryable()
            .Where(p => p.UserId == userId && p.Status == "pending" && ids.Contains(p.Id))
            .ToListAsync(ct);

    public Task<bool> ExistsForRecurrenceOnDateAsync(
        Guid recurringTransactionId, DateOnly occurredOn, CancellationToken ct = default)
        => Queryable().AnyAsync(
            p => p.RecurringTransactionId == recurringTransactionId && p.OccurredOn == occurredOn, ct);
}
