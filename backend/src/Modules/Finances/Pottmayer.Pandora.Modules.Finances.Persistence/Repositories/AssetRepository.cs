using Microsoft.EntityFrameworkCore;
using Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Finances.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Finances.Domain.ValueObjects;
using Pottmayer.Tars.Data.Abstractions.DataContext;
using Pottmayer.Tars.Data.Relational.Repositories;

namespace Pottmayer.Pandora.Modules.Finances.Persistence.Repositories;

public sealed class AssetRepository(IDataContextAccessor accessor)
    : StandardRepository<Asset, Guid>(accessor), IAssetRepository
{
    public Task<Asset?> FindByIdForUserAsync(Guid id, Guid userId, CancellationToken ct = default)
        => Queryable().FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId, ct);

    public async Task<IReadOnlyList<Asset>> ListAsync(Guid userId, Guid? assetClassId, string? search, CancellationToken ct = default)
    {
        var query = Queryable().Where(a => a.UserId == userId);
        if (assetClassId is not null)
            query = query.Where(a => a.SystemAssetClassId == assetClassId || a.UserAssetClassId == assetClassId);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search.Trim()}%";
            query = query.Where(a => EF.Functions.ILike(a.Name, pattern) || (a.Code != null && EF.Functions.ILike(a.Code, pattern)));
        }
        return await query.OrderBy(a => a.Name).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Asset>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, Guid userId, CancellationToken ct = default)
        => ids.Count == 0 ? [] : await Queryable().Where(a => a.UserId == userId && ids.Contains(a.Id)).ToListAsync(ct);

    public Task<bool> ExistsWithNameAsync(Guid userId, string name, Guid? excludingId, CancellationToken ct = default)
    {
        var trimmed = name.Trim();
        var query = Queryable().Where(a => a.UserId == userId && a.Name == trimmed);
        if (excludingId is not null) query = query.Where(a => a.Id != excludingId);
        return query.AnyAsync(ct);
    }

    public Task<bool> AnyForClassAsync(Guid assetClassId, CancellationToken ct = default)
        => Queryable().AnyAsync(a => a.SystemAssetClassId == assetClassId || a.UserAssetClassId == assetClassId, ct);

    public Task<bool> ExistsWithCodeAsync(Guid userId, Guid assetClassId, string code, Guid? excludingId, CancellationToken ct = default)
    {
        var normalized = Asset.NormalizeCode(code);
        var query = Queryable().Where(a => a.UserId == userId && a.Code == normalized
            && (a.SystemAssetClassId == assetClassId || a.UserAssetClassId == assetClassId));
        if (excludingId is not null) query = query.Where(a => a.Id != excludingId);
        return query.AnyAsync(ct);
    }
}
