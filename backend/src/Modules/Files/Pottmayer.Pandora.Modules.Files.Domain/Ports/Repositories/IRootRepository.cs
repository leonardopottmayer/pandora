using Pottmayer.Pandora.Modules.Files.Domain.Entities;
using Pottmayer.Tars.Data.Abstractions.Repositories;

namespace Pottmayer.Pandora.Modules.Files.Domain.Ports.Repositories;

/// <summary>Every read loads the root's selection marks with it.</summary>
public interface IRootRepository : IStandardRepository<Root, Guid>
{
    /// <summary>Active or removed.</summary>
    Task<Root?> FindForUserAsync(Guid id, Guid userId, CancellationToken ct = default);

    Task<Root?> FindActiveForDeviceAsync(Guid id, Guid deviceId, CancellationToken ct = default);

    /// <summary>Active or removed: the same path added again brings a removed root back.</summary>
    Task<Root?> FindByDevicePathAsync(Guid deviceId, string localPath, CancellationToken ct = default);

    Task<IReadOnlyList<Root>> ListActiveByUserAsync(Guid userId, CancellationToken ct = default);

    Task<IReadOnlyList<Root>> ListActiveByDeviceAsync(Guid deviceId, CancellationToken ct = default);
}
