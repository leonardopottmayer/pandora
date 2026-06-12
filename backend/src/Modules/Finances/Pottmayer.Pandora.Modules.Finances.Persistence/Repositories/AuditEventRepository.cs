using Microsoft.EntityFrameworkCore;
using Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Finances.Domain.Ports.Repositories;
using Pottmayer.Tars.Data.Abstractions.DataContext;
using Pottmayer.Tars.Data.Relational.Repositories;

namespace Pottmayer.Pandora.Modules.Finances.Persistence.Repositories;

public sealed class AuditEventRepository(IDataContextAccessor accessor)
    : StandardRepository<AuditEvent, Guid>(accessor), IAuditEventRepository
{
    public async Task<IReadOnlyList<AuditEvent>> GetByEntityAsync(
        Guid userId, string entityType, Guid entityId, int skip, int take, CancellationToken ct = default) =>
        await Queryable()
            .Where(e => e.UserId == userId && e.EntityType == entityType && e.EntityId == entityId)
            .OrderByDescending(e => e.OccurredAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<AuditEvent>> GetByCorrelationAsync(
        Guid userId, Guid correlationId, int skip, int take, CancellationToken ct = default) =>
        await Queryable()
            .Where(e => e.UserId == userId && e.CorrelationId == correlationId)
            .OrderBy(e => e.OccurredAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(ct);
}
