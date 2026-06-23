using Microsoft.EntityFrameworkCore;
using Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Finances.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Finances.Domain.ValueObjects;
using Pottmayer.Tars.Data.Abstractions.DataContext;
using Pottmayer.Tars.Data.Relational.Repositories;

namespace Pottmayer.Pandora.Modules.Finances.Persistence.Repositories;

public sealed class RecurringTransactionRepository(IDataContextAccessor accessor)
    : StandardRepository<RecurringTransaction, Guid>(accessor), IRecurringTransactionRepository
{
    public Task<RecurringTransaction?> FindByIdForUserAsync(Guid id, Guid userId, CancellationToken ct = default)
        => Queryable().FirstOrDefaultAsync(r => r.Id == id && r.UserId == userId, ct);

    public async Task<IReadOnlyList<RecurringTransaction>> GetAllForUserAsync(Guid userId, CancellationToken ct = default)
        => await Queryable()
            .Where(r => r.UserId == userId)
            .OrderBy(r => r.Name)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<RecurringTransaction>> GetActiveWithOccurrencesBeforeAsync(
        DateOnly horizon, CancellationToken ct = default)
        => await Queryable()
            .Where(r => r.Status == RecurringTransactionStatus.Active && r.AutoGenerate && r.NextOccurrenceOn <= horizon)
            .ToListAsync(ct);
}
