using Microsoft.EntityFrameworkCore;
using Pottmayer.Pandora.Modules.Identity.Domain.Entities;
using Pottmayer.Pandora.Modules.Identity.Domain.Ports.Repositories;
using Pottmayer.Tars.Data.Abstractions.DataContext;
using Pottmayer.Tars.Data.Relational.Repositories;

namespace Pottmayer.Pandora.Modules.Identity.Persistence.Repositories;

public sealed class DeviceRepository(IDataContextAccessor accessor)
    : StandardRepository<Device, Guid>(accessor), IDeviceRepository
{
    public Task<Device?> FindActiveByKeyHashAsync(string keyHash, CancellationToken ct = default)
        => Queryable().FirstOrDefaultAsync(d => d.KeyHash == keyHash && d.RevokedAt == null, ct);

    public async Task<IReadOnlyList<Device>> ListActiveByUserAsync(Guid userId, CancellationToken ct = default)
        => await Queryable()
            .Where(d => d.UserId == userId && d.RevokedAt == null)
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync(ct);
}
