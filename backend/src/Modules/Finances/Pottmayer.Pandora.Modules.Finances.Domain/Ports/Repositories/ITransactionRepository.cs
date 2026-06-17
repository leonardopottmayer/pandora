using Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;
using Pottmayer.Tars.Data.Relational.Abstractions.Repositories;

namespace Pottmayer.Pandora.Modules.Finances.Domain.Ports.Repositories;

/// <summary>Filters for the account statement (extrato). All members are optional (AND-combined).</summary>
public sealed record TransactionFilter(
    Guid? AccountId = null,
    Guid? CardStatementId = null,
    DateOnly? From = null,
    DateOnly? To = null,
    string? Kind = null,
    string? Status = null,
    Guid? SystemCategoryId = null,
    Guid? UserCategoryId = null,
    string? Text = null,
    string? Origin = null,
    IReadOnlyCollection<Guid>? Ids = null,
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

    Task<IReadOnlyList<Transaction>> GetByStatementAsync(Guid statementId, Guid userId, CancellationToken ct = default);

    /// <summary>All installment transactions of a plan, ordered by installment number.</summary>
    Task<IReadOnlyList<Transaction>> GetByInstallmentPlanAsync(Guid installmentPlanId, Guid userId, CancellationToken ct = default);

    Task<decimal> GetStatementTotalAsync(Guid statementId, Guid userId, CancellationToken ct = default);

    Task<decimal> GetStatementPaidTotalAsync(Guid statementId, Guid userId, CancellationToken ct = default);

    Task<decimal> GetUnpaidStatementTotalForCardAsync(Guid cardId, Guid userId, CancellationToken ct = default);

    /// <summary>Whether some transaction already declares <paramref name="transactionId"/> as the one it reverses.</summary>
    Task<bool> ExistsReversalForAsync(Guid transactionId, Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Returns all pending account-targeted transactions whose <c>occurred_on</c> is on or before
    /// <paramref name="today"/>. Used by the daily job to auto-post scheduled entries.
    /// </summary>
    Task<IReadOnlyList<Transaction>> GetDuePendingAsync(DateOnly today, CancellationToken ct = default);
}
