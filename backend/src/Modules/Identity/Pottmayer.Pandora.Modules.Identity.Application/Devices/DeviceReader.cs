using Pottmayer.Pandora.Modules.Identity.Abstractions;
using Pottmayer.Pandora.Modules.Identity.Abstractions.Models;
using Pottmayer.Pandora.Modules.Identity.Abstractions.Ports;
using Pottmayer.Pandora.Modules.Identity.Domain.Ports.Repositories;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Identity.Application.Devices;

/// <summary>Read-only device snapshots for other modules. No aggregate crosses this boundary.</summary>
public sealed class DeviceReader(IUnitOfWorkFactory factory) : IDeviceReader
{
    public async Task<DeviceSnapshot?> GetActiveAsync(Guid userId, Guid deviceId, CancellationToken ct = default)
    {
        var device = await factory.ExecuteAsync(IdentityModule.DatabaseKey, (ctx, token) =>
            ctx.AcquireRepository<IDeviceRepository>().GetByIdAsync(deviceId, token), cancellationToken: ct);

        return device is { IsActive: true } && device.UserId == userId
            ? new DeviceSnapshot(device.Id, device.Name, device.Platform.Value)
            : null;
    }
}
