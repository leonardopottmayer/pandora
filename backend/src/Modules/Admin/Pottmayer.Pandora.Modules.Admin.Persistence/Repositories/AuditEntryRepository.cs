using Microsoft.EntityFrameworkCore;
using Pottmayer.Pandora.Modules.Admin.Domain.Entities;
using Pottmayer.Pandora.Modules.Admin.Domain.Ports.Repositories;
using Pottmayer.Tars.Data.Abstractions.DataContext;
using Pottmayer.Tars.Data.Relational.Repositories;

namespace Pottmayer.Pandora.Modules.Admin.Persistence.Repositories;

public sealed class AuditEntryRepository(IDataContextAccessor accessor)
    : StandardRepository<AuditEntry, Guid>(accessor), IAuditEntryRepository
{
    public async Task<IReadOnlyList<AuditEntry>> ListAsync(
        Guid? actorId, string? targetType, string? targetId, DateTimeOffset? before, int limit,
        CancellationToken ct = default)
    {
        var query = Queryable().AsNoTracking();
        if (actorId is { } actor)
            query = query.Where(e => e.ActorId == actor);
        if (!string.IsNullOrWhiteSpace(targetType))
            query = query.Where(e => e.TargetType == targetType);
        if (!string.IsNullOrWhiteSpace(targetId))
            query = query.Where(e => e.TargetId == targetId);
        if (before is { } instant)
            query = query.Where(e => e.OccurredAt < instant);

        return await query.OrderByDescending(e => e.OccurredAt).Take(limit).ToListAsync(ct);
    }
}
