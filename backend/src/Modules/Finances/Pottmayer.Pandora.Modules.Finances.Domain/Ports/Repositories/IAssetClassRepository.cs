using Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;
using Pottmayer.Tars.Data.Abstractions.Repositories;

namespace Pottmayer.Pandora.Modules.Finances.Domain.Ports.Repositories;

public interface IAssetClassRepository : IStandardRepository<AssetClass, Guid>
{
    /// <summary>A system class or one of the user's own.</summary>
    Task<AssetClass?> FindVisibleAsync(Guid id, Guid userId, CancellationToken ct = default);

    /// <summary>System classes plus the user's own, archived ones included, by name.</summary>
    Task<IReadOnlyList<AssetClass>> ListVisibleAsync(Guid userId, CancellationToken ct = default);

    Task<bool> ExistsWithNameAsync(Guid userId, string name, Guid? excludingId, CancellationToken ct = default);
}
