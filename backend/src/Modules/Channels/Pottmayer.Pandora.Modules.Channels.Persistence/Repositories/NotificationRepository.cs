using Microsoft.EntityFrameworkCore;
using Pottmayer.Pandora.Modules.Channels.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Channels.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Channels.Domain.ValueObjects;
using Pottmayer.Tars.Data.Abstractions.DataContext;
using Pottmayer.Tars.Data.Relational.Repositories;

namespace Pottmayer.Pandora.Modules.Channels.Persistence.Repositories;

public sealed class NotificationRepository(IDataContextAccessor accessor)
    : StandardRepository<Notification, Guid>(accessor), INotificationRepository
{
    public Task<bool> ExistsByCorrelationIdAsync(Guid correlationId, CancellationToken ct = default) =>
        Queryable().AnyAsync(n => n.CorrelationId == correlationId, ct);

    public async Task<IReadOnlyList<Notification>> GetDueAsync(DateTimeOffset now, int batchSize, CancellationToken ct = default)
    {
        var due = await Queryable()
            .Where(n => (n.Status == NotificationStatus.Pending || n.Status == NotificationStatus.Failed)
                        && n.NextAttemptAt <= now)
            .OrderBy(n => n.NextAttemptAt)
            .Take(batchSize)
            .ToListAsync(ct);

        return due;
    }
}
