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
