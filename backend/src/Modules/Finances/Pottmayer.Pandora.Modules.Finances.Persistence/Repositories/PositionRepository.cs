using Microsoft.EntityFrameworkCore;
using Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Finances.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Finances.Domain.ValueObjects;
using Pottmayer.Tars.Data.Abstractions.DataContext;
using Pottmayer.Tars.Data.Relational.Repositories;

namespace Pottmayer.Pandora.Modules.Finances.Persistence.Repositories;

public sealed class PositionRepository(IDataContextAccessor accessor)
    : StandardRepository<Position, Guid>(accessor), IPositionRepository
{
    public Task<Position?> FindByIdForUserAsync(Guid id, Guid userId, CancellationToken ct = default)
        => Queryable().FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId, ct);

    public async Task<IReadOnlyList<Position>> ListAsync(Guid userId, Guid? accountId, bool openOnly, CancellationToken ct = default)
    {
        var query = Queryable().Where(p => p.UserId == userId);
        if (accountId is not null) query = query.Where(p => p.AccountId == accountId);
        if (openOnly) query = query.Where(p => p.Status == PositionStatus.Open);
        return await query.OrderBy(p => p.CreatedAt).ToListAsync(ct);
    }

    public Task<Position?> FindOpenUnitAsync(Guid accountId, Guid assetId, CancellationToken ct = default)
        => Queryable().FirstOrDefaultAsync(p =>
            p.AccountId == accountId && p.AssetId == assetId
            && p.Behavior == AssetBehavior.Unit && p.Status == PositionStatus.Open, ct);

    public Task<bool> AnyForAccountAsync(Guid accountId, CancellationToken ct = default)
        => Queryable().AnyAsync(p => p.AccountId == accountId, ct);

    public Task<bool> AnyForAssetAsync(Guid assetId, CancellationToken ct = default)
        => Queryable().AnyAsync(p => p.AssetId == assetId, ct);
}
