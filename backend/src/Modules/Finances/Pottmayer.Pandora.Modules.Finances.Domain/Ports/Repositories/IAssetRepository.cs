using Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;
using Pottmayer.Tars.Data.Abstractions.Repositories;

namespace Pottmayer.Pandora.Modules.Finances.Domain.Ports.Repositories;

public interface IAssetRepository : IStandardRepository<Asset, Guid>
{
    Task<Asset?> FindByIdForUserAsync(Guid id, Guid userId, CancellationToken ct = default);

    Task<IReadOnlyList<Asset>> ListAsync(Guid userId, Guid? assetClassId, string? search, CancellationToken ct = default);

    Task<IReadOnlyList<Asset>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, Guid userId, CancellationToken ct = default);

    Task<bool> ExistsWithNameAsync(Guid userId, string name, Guid? excludingId, CancellationToken ct = default);

    /// <summary>True when any asset, archived or not, belongs to the class.</summary>
    Task<bool> AnyForClassAsync(Guid assetClassId, CancellationToken ct = default);

    Task<bool> ExistsWithCodeAsync(Guid userId, Guid assetClassId, string code, Guid? excludingId, CancellationToken ct = default);
}
