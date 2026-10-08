using Microsoft.EntityFrameworkCore;
using Pottmayer.Pandora.Modules.Files.Domain.Entities;
using Pottmayer.Pandora.Modules.Files.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Files.Domain.ValueObjects;
using Pottmayer.Tars.Data.Abstractions.DataContext;
using Pottmayer.Tars.Data.Relational.Repositories;

namespace Pottmayer.Pandora.Modules.Files.Persistence.Repositories;

public sealed class RootRepository(IDataContextAccessor accessor)
    : StandardRepository<Root, Guid>(accessor), IRootRepository
{
    private IQueryable<Root> WithMarks => Set.Include(r => r.Marks);

    public Task<Root?> FindForUserAsync(Guid id, Guid userId, CancellationToken ct = default) =>
        WithMarks.FirstOrDefaultAsync(r => r.Id == id && r.UserId == userId, ct);

    public Task<Root?> FindActiveForDeviceAsync(Guid id, Guid deviceId, CancellationToken ct = default) =>
        WithMarks.FirstOrDefaultAsync(r => r.Id == id && r.DeviceId == deviceId && r.Status == RootStatus.Active, ct);

    public Task<Root?> FindByDevicePathAsync(Guid deviceId, string localPath, CancellationToken ct = default) =>
        WithMarks.FirstOrDefaultAsync(r => r.DeviceId == deviceId && r.LocalPath == localPath, ct);

    public async Task<IReadOnlyList<Root>> ListActiveByUserAsync(Guid userId, CancellationToken ct = default) =>
        await WithMarks.Where(r => r.UserId == userId && r.Status == RootStatus.Active)
                       .OrderBy(r => r.Name)
                       .ToListAsync(ct);

    public async Task<IReadOnlyList<Root>> ListActiveByDeviceAsync(Guid deviceId, CancellationToken ct = default) =>
        await WithMarks.Where(r => r.DeviceId == deviceId && r.Status == RootStatus.Active)
                       .OrderBy(r => r.Name)
                       .ToListAsync(ct);
}
