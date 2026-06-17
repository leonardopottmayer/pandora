using Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;
using Pottmayer.Tars.Data.Relational.Abstractions.Repositories;

namespace Pottmayer.Pandora.Modules.Finances.Domain.Ports.Repositories;

public sealed record PendingTransactionFilter(
    string? Source = null,
    Guid? AccountId = null,
    Guid? CardId = null,
    DateOnly? From = null,
    DateOnly? To = null,
    int Skip = 0,
    int Take = 50);

public interface IPendingTransactionRepository : IStandardRepository<PendingTransaction, Guid>
{
    Task<PendingTransaction?> FindByIdForUserAsync(Guid id, Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<PendingTransaction>> QueryAsync(Guid userId, PendingTransactionFilter filter, CancellationToken ct = default);
    Task<IReadOnlyList<PendingTransaction>> GetPendingByIdsForUserAsync(Guid userId, IReadOnlyCollection<Guid> ids, CancellationToken ct = default);

    /// <summary>
    /// Checks whether a pending transaction already exists for the given recurrence and occurrence date.
    /// Used to enforce idempotency in the generation job.
    /// </summary>
    Task<bool> ExistsForRecurrenceOnDateAsync(Guid recurringTransactionId, DateOnly occurredOn, CancellationToken ct = default);
}
