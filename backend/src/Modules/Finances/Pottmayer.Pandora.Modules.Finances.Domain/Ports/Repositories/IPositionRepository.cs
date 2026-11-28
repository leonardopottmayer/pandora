using Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;
using Pottmayer.Tars.Data.Abstractions.Repositories;

namespace Pottmayer.Pandora.Modules.Finances.Domain.Ports.Repositories;

public interface IPositionRepository : IStandardRepository<Position, Guid>
{
    Task<Position?> FindByIdForUserAsync(Guid id, Guid userId, CancellationToken ct = default);

    /// <summary>The user's positions, optionally of one account and/or only the open ones.</summary>
    Task<IReadOnlyList<Position>> ListAsync(Guid userId, Guid? accountId, bool openOnly, CancellationToken ct = default);

    /// <summary>The open unit position of an asset in an account, if any (there is at most one).</summary>
    Task<Position?> FindOpenUnitAsync(Guid accountId, Guid assetId, CancellationToken ct = default);

    Task<bool> AnyForAccountAsync(Guid accountId, CancellationToken ct = default);

    /// <summary>True when the asset has any position, open or closed.</summary>
    Task<bool> AnyForAssetAsync(Guid assetId, CancellationToken ct = default);
}
