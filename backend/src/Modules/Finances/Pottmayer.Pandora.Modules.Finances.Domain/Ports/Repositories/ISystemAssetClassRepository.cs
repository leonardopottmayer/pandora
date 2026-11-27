using Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;
using Pottmayer.Tars.Data.Abstractions.Repositories;

namespace Pottmayer.Pandora.Modules.Finances.Domain.Ports.Repositories;

public interface ISystemAssetClassRepository : IStandardRepository<SystemAssetClass, Guid>
{
    Task<SystemAssetClass?> FindAsync(Guid id, CancellationToken ct = default);

    /// <summary>Every system class, in display order.</summary>
    Task<IReadOnlyList<SystemAssetClass>> ListAsync(CancellationToken ct = default);
}
