using Microsoft.EntityFrameworkCore;
using Pottmayer.Pandora.Modules.Agenda.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Agenda.Domain.Ports.Repositories;
using Pottmayer.Tars.Data.Abstractions.DataContext;
using Pottmayer.Tars.Data.Relational.Repositories;

namespace Pottmayer.Pandora.Modules.Agenda.Persistence.Repositories;

public sealed class SyncConflictRepository(IDataContextAccessor accessor)
    : StandardRepository<SyncConflict, Guid>(accessor), ISyncConflictRepository
{
    public async Task<IReadOnlyList<SyncConflict>> GetRecentAsync(Guid userId, int limit, CancellationToken ct = default) =>
        await Queryable().Where(c => c.UserId == userId).OrderByDescending(c => c.DetectedAt).Take(limit).ToListAsync(ct);
}
