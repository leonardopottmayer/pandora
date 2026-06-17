using Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;
using Pottmayer.Tars.Data.Relational.Abstractions.Repositories;

namespace Pottmayer.Pandora.Modules.Finances.Domain.Ports.Repositories;

public interface IRecurringTransactionRepository : IStandardRepository<RecurringTransaction, Guid>
{
    Task<RecurringTransaction?> FindByIdForUserAsync(Guid id, Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<RecurringTransaction>> GetAllForUserAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Returns all active recurring transactions whose <c>next_occurrence_on</c> is on or before
    /// <paramref name="horizon"/>. Used by the generation job to find what to process.
    /// </summary>
    Task<IReadOnlyList<RecurringTransaction>> GetActiveWithOccurrencesBeforeAsync(DateOnly horizon, CancellationToken ct = default);
}
