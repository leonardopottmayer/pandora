using Pottmayer.Pandora.Modules.Notifications.Domain.Aggregates;
using Pottmayer.Tars.Data.Relational.Abstractions.Repositories;

namespace Pottmayer.Pandora.Modules.Notifications.Domain.Ports.Repositories;

public interface INotificationRepository : IStandardRepository<Notification, Guid>
{
    /// <summary>Idempotency guard for enqueue: whether a notification already exists for the given event.</summary>
    Task<bool> ExistsByCorrelationIdAsync(Guid correlationId, CancellationToken ct = default);

    /// <summary>Loads notifications due for dispatch (Pending/Failed with NextAttemptAt &lt;= now), oldest first.</summary>
    Task<IReadOnlyList<Notification>> GetDueAsync(DateTimeOffset now, int batchSize, CancellationToken ct = default);
}
