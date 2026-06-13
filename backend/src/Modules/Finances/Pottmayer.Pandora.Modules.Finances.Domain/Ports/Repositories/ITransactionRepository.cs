using Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;
using Pottmayer.Tars.Data.Relational.Abstractions.Repositories;

namespace Pottmayer.Pandora.Modules.Finances.Domain.Ports.Repositories;

/// <summary>Filters for the account statement (extrato). All members are optional (AND-combined).</summary>
public sealed record TransactionFilter(
    Guid? AccountId = null,
    DateOnly? From = null,
    DateOnly? To = null,
    string? Kind = null,
    string? Status = null,
    Guid? SystemCategoryId = null,
    Guid? UserCategoryId = null,
    string? Text = null,
    string? Origin = null,
    int Skip = 0,
    int Take = 50);

public interface ITransactionRepository : IStandardRepository<Transaction, Guid>
{
    /// <summary>One transaction owned by the user, or <c>null</c> (404-on-foreign-resource rule).</summary>
    Task<Transaction?> FindByIdForUserAsync(Guid id, Guid userId, CancellationToken ct = default);

    /// <summary>Both legs of a transfer (or whatever shares the group id), scoped to the user.</summary>
    Task<IReadOnlyList<Transaction>> GetByTransferGroupAsync(
        Guid transferGroupId, Guid userId, CancellationToken ct = default);

    /// <summary>The statement page, ordered by <c>occurred_on</c> then <c>id</c> for stable paging.</summary>
    Task<IReadOnlyList<Transaction>> QueryAsync(
        Guid userId, TransactionFilter filter, CancellationToken ct = default);

    /// <summary>Signed sum of the account's <c>posted</c> entries — the current balance (D1).</summary>
    Task<decimal> GetPostedBalanceAsync(Guid accountId, Guid userId, CancellationToken ct = default);

    /// <summary>Balance including <c>pending</c> (scheduled/future) entries — the projected balance.</summary>
    Task<decimal> GetProjectedBalanceAsync(Guid accountId, Guid userId, CancellationToken ct = default);
}
