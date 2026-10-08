using Microsoft.EntityFrameworkCore;
using Pottmayer.Pandora.Modules.Files.Domain.Entities;
using Pottmayer.Pandora.Modules.Files.Domain.Ports.Repositories;
using Pottmayer.Tars.Data.Abstractions.DataContext;
using Pottmayer.Tars.Data.Relational.Repositories;

namespace Pottmayer.Pandora.Modules.Files.Persistence.Repositories;

public sealed class FilterRepository(IDataContextAccessor accessor)
    : StandardRepository<Filter, Guid>(accessor), IFilterRepository
{
    public async Task<IReadOnlyList<Filter>> ListByUserAsync(Guid userId, CancellationToken ct = default) =>
        await Set.Where(f => f.UserId == userId)
                 .OrderByDescending(f => f.IsBuiltin)
                 .ThenBy(f => f.Name)
                 .ToListAsync(ct);

    public Task<Filter?> FindForUserAsync(Guid id, Guid userId, CancellationToken ct = default) =>
        Set.FirstOrDefaultAsync(f => f.Id == id && f.UserId == userId, ct);

    public async Task<IReadOnlyList<Filter>> ListEnabledForRootAsync(
        Guid userId, Guid deviceId, Guid rootId, CancellationToken ct = default) =>
        await Set.AsNoTracking()
                 .Where(f => f.UserId == userId && f.IsEnabled
                             && (f.DeviceId == null || f.DeviceId == deviceId)
                             && (f.RootId == null || f.RootId == rootId))
                 .ToListAsync(ct);
}
