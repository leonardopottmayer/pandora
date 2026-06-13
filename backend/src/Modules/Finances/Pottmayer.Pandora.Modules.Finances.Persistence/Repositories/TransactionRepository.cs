using Microsoft.EntityFrameworkCore;
using Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Finances.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Finances.Domain.ValueObjects;
using Pottmayer.Tars.Data.Abstractions.DataContext;
using Pottmayer.Tars.Data.Relational.Repositories;

namespace Pottmayer.Pandora.Modules.Finances.Persistence.Repositories;

public sealed class TransactionRepository(IDataContextAccessor accessor)
    : StandardRepository<Transaction, Guid>(accessor), ITransactionRepository
{
    public Task<Transaction?> FindByIdForUserAsync(Guid id, Guid userId, CancellationToken ct = default)
        => Queryable().FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId, ct);

    public async Task<IReadOnlyList<Transaction>> GetByTransferGroupAsync(
        Guid transferGroupId, Guid userId, CancellationToken ct = default)
        => await Queryable()
            .Where(t => t.TransferGroupId == transferGroupId && t.UserId == userId)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Transaction>> QueryAsync(
        Guid userId, TransactionFilter filter, CancellationToken ct = default)
    {
        var query = Queryable().Where(t => t.UserId == userId);

        if (filter.AccountId is not null)
            query = query.Where(t => t.AccountId == filter.AccountId);
        if (filter.CardStatementId is not null)
            query = query.Where(t => t.CardStatementId == filter.CardStatementId);
        if (filter.From is not null)
            query = query.Where(t => t.OccurredOn >= filter.From);
        if (filter.To is not null)
            query = query.Where(t => t.OccurredOn <= filter.To);
        if (filter.Kind is not null)
            query = query.Where(t => t.Kind == TransactionKind.FromValue(filter.Kind));
        if (filter.Status is not null)
            query = query.Where(t => t.Status == TransactionStatus.FromValue(filter.Status));
        if (filter.SystemCategoryId is not null)
            query = query.Where(t => t.SystemCategoryId == filter.SystemCategoryId);
        if (filter.UserCategoryId is not null)
            query = query.Where(t => t.UserCategoryId == filter.UserCategoryId);
        if (filter.Origin is not null)
            query = query.Where(t => t.Origin == filter.Origin);
        if (!string.IsNullOrWhiteSpace(filter.Text))
        {
            var text = filter.Text.Trim().ToLower();
            query = query.Where(t =>
                t.Description.ToLower().Contains(text) ||
                (t.Payee != null && t.Payee.ToLower().Contains(text)));
        }

        return await query
            .OrderByDescending(t => t.OccurredOn)
            .ThenByDescending(t => t.Id)
            .Skip(filter.Skip)
            .Take(filter.Take)
            .ToListAsync(ct);
    }

    public Task<decimal> GetPostedBalanceAsync(Guid accountId, Guid userId, CancellationToken ct = default)
        => SumSignedAsync(accountId, userId, includePending: false, ct);

    public Task<decimal> GetProjectedBalanceAsync(Guid accountId, Guid userId, CancellationToken ct = default)
        => SumSignedAsync(accountId, userId, includePending: true, ct);

    public async Task<IReadOnlyList<Transaction>> GetByStatementAsync(
        Guid statementId, Guid userId, CancellationToken ct = default)
        => await Queryable()
            .Where(t => t.CardStatementId == statementId && t.UserId == userId)
            .OrderBy(t => t.OccurredOn)
            .ThenBy(t => t.Id)
            .ToListAsync(ct);

    public async Task<decimal> GetStatementTotalAsync(
        Guid statementId, Guid userId, CancellationToken ct = default)
    {
        var rows = await Queryable()
            .Where(t => t.CardStatementId == statementId && t.UserId == userId && t.Status == TransactionStatus.Posted)
            .Select(t => new { t.Kind, t.Amount })
            .ToListAsync(ct);

        return rows.Sum(r => r.Amount * r.Kind.StatementSign);
    }

    public async Task<decimal> GetStatementPaidTotalAsync(
        Guid statementId, Guid userId, CancellationToken ct = default)
    {
        var rows = await Queryable()
            .Where(t => t.PaidStatementId == statementId && t.UserId == userId && t.Status == TransactionStatus.Posted)
            .Select(t => t.Amount)
            .ToListAsync(ct);

        return rows.Sum();
    }

    public async Task<decimal> GetUnpaidStatementTotalForCardAsync(
        Guid cardId, Guid userId, CancellationToken ct = default)
    {
        var rows = await Queryable()
            .Where(t => t.CardId == cardId && t.UserId == userId && t.CardStatementId != null && t.Status == TransactionStatus.Posted)
            .Select(t => new { t.CardStatementId, t.Kind, t.Amount })
            .ToListAsync(ct);

        var paid = await Queryable()
            .Where(t => t.PaidStatementId != null && t.UserId == userId)
            .Select(t => new { t.PaidStatementId, t.Amount, t.Status })
            .ToListAsync(ct);

        var totalsByStatement = rows
            .GroupBy(r => r.CardStatementId!.Value)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Amount * x.Kind.StatementSign));

        var paidByStatement = paid
            .Where(x => x.Status == TransactionStatus.Posted)
            .GroupBy(x => x.PaidStatementId!.Value)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Amount));

        return totalsByStatement.Sum(pair => Math.Max(0m, pair.Value - paidByStatement.GetValueOrDefault(pair.Key)));
    }

    /// <summary>
    /// Signed sum over the account ledger. Kept in memory because the sign is a function of the kind
    /// (a value object); ledger growth is addressed later with balance snapshots (D1), not now.
    /// </summary>
    private async Task<decimal> SumSignedAsync(
        Guid accountId, Guid userId, bool includePending, CancellationToken ct)
    {
        var query = Queryable().Where(t => t.AccountId == accountId && t.UserId == userId);
        query = includePending
            ? query.Where(t => t.Status != TransactionStatus.Void)
            : query.Where(t => t.Status == TransactionStatus.Posted);

        var rows = await query
            .Select(t => new { t.Kind, t.Amount })
            .ToListAsync(ct);

        return rows.Sum(r => r.Amount * r.Kind.Sign);
    }
}
