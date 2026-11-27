using Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;
using Pottmayer.Tars.Data.Abstractions.Repositories;

namespace Pottmayer.Pandora.Modules.Finances.Domain.Ports.Repositories;

public interface IUserAssetClassRepository : IStandardRepository<UserAssetClass, Guid>
{
    Task<UserAssetClass?> FindByIdForUserAsync(Guid id, Guid userId, CancellationToken ct = default);

    /// <summary>The user's classes, archived ones included, by name.</summary>
    Task<IReadOnlyList<UserAssetClass>> ListByUserAsync(Guid userId, CancellationToken ct = default);

    Task<bool> ExistsWithNameAsync(Guid userId, string name, Guid? excludingId, CancellationToken ct = default);
}
