using Pottmayer.Pandora.Modules.Channels.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Channels.Domain.ValueObjects;
using Pottmayer.Tars.Data.Relational.Abstractions.Repositories;

namespace Pottmayer.Pandora.Modules.Channels.Domain.Ports.Repositories;

public interface INotificationRepository : IStandardRepository<Notification, Guid>
{
    /// <summary>
    /// Idempotency guard for enqueue: whether a notification already exists for the given event on
    /// this channel. Keyed on the pair because one request fans out across channels under a shared
    /// correlation id.
    /// </summary>
    Task<bool> ExistsByCorrelationAndChannelAsync(Guid correlationId, Channel channel, CancellationToken ct = default);

    /// <summary>Loads notifications due for dispatch (Pending/Failed with NextAttemptAt &lt;= now), oldest first.</summary>
    Task<IReadOnlyList<Notification>> GetDueAsync(DateTimeOffset now, int batchSize, CancellationToken ct = default);

    /// <summary>
    /// A page of the user's delivery history, newest first, narrowed by the optional filters.
    /// </summary>
    Task<IReadOnlyList<Notification>> GetHistoryAsync(
        Guid userId,
        NotificationStatus? status,
        Channel? channel,
        string? category,
        DateTimeOffset? from,
        DateTimeOffset? to,
        int skip,
        int take,
        CancellationToken ct = default);
}
