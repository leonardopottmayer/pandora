using Microsoft.EntityFrameworkCore;
using Pottmayer.Pandora.Modules.Files.Domain.Entities;
using Pottmayer.Pandora.Modules.Files.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Files.Domain.ValueObjects;
using Pottmayer.Tars.Data.Abstractions.DataContext;
using Pottmayer.Tars.Data.Relational.Repositories;

namespace Pottmayer.Pandora.Modules.Files.Persistence.Repositories;

public sealed class ScanRepository(IDataContextAccessor accessor)
    : StandardRepository<Scan, Guid>(accessor), IScanRepository
{
    public async Task<IReadOnlyList<Scan>> ListOpenByRootAsync(Guid rootId, CancellationToken ct = default) =>
        await Set.Where(s => s.RootId == rootId && (s.Status == ScanStatus.Running || s.Status == ScanStatus.Held))
                 .ToListAsync(ct);

    public async Task<IReadOnlyList<Scan>> ListByRootAsync(Guid rootId, int limit, CancellationToken ct = default) =>
        await Set.AsNoTracking()
                 .Where(s => s.RootId == rootId)
                 .OrderByDescending(s => s.StartedAt)
                 .Take(limit)
                 .ToListAsync(ct);

    public async Task<IReadOnlyList<Scan>> ListStaleRunningAsync(DateTimeOffset cutoff, CancellationToken ct = default) =>
        await Set.Where(s => s.Status == ScanStatus.Running && (s.LastBatchAt ?? s.StartedAt) < cutoff)
                 .ToListAsync(ct);
}
