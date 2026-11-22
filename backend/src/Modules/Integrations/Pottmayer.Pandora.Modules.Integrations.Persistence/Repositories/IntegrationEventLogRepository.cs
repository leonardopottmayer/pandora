using Microsoft.EntityFrameworkCore;
using Pottmayer.Pandora.Modules.Integrations.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Integrations.Domain.Ports.Repositories;
using Pottmayer.Tars.Data.Abstractions.DataContext;
using Pottmayer.Tars.Data.Relational.Repositories;

namespace Pottmayer.Pandora.Modules.Integrations.Persistence.Repositories;

public sealed class IntegrationEventLogRepository(IDataContextAccessor accessor)
    : StandardRepository<IntegrationEventLogEntry, Guid>(accessor), IIntegrationEventLogRepository
{
    public async Task<IReadOnlyList<IntegrationEventLogEntry>> GetRecentByUserAsync(
        Guid userId, int limit, CancellationToken ct = default) =>
        await Queryable()
            .Where(e => e.UserId == userId)
            .OrderByDescending(e => e.OccurredAt)
            .Take(limit)
            .ToListAsync(ct);
}
