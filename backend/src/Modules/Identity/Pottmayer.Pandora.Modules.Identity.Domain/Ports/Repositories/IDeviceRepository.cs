using Pottmayer.Pandora.Modules.Identity.Domain.Entities;
using Pottmayer.Tars.Data.Abstractions.Repositories;

namespace Pottmayer.Pandora.Modules.Identity.Domain.Ports.Repositories;

public interface IDeviceRepository : IStandardRepository<Device, Guid>
{
    /// <summary>The non-revoked device holding this key, if any.</summary>
    Task<Device?> FindActiveByKeyHashAsync(string keyHash, CancellationToken ct = default);

    /// <summary>The user's non-revoked devices, newest first.</summary>
    Task<IReadOnlyList<Device>> ListActiveByUserAsync(Guid userId, CancellationToken ct = default);
}
