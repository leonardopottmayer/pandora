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
    public Task<bool> ExistsByCorrelationAndChannelAsync(Guid correlationId, Channel channel, CancellationToken ct = default) =>
        Queryable().AnyAsync(n => n.CorrelationId == correlationId && n.Channel == channel, ct);

    public async Task<long> CountPendingAsync(CancellationToken ct = default) =>
        await Queryable()
            .LongCountAsync(
                n => n.Status == NotificationStatus.Pending
                     || n.Status == NotificationStatus.Failed
                     || n.Status == NotificationStatus.Sending,
                ct);

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

    public async Task<IReadOnlyList<Notification>> GetHistoryAsync(
        Guid userId,
        NotificationStatus? status,
        Channel? channel,
        string? category,
        DateTimeOffset? from,
        DateTimeOffset? to,
        int skip,
        int take,
        CancellationToken ct = default)
    {
        var query = Queryable().Where(n => n.UserId == userId);

        if (status is { } s)
            query = query.Where(n => n.Status == s);
        if (channel is { } c)
            query = query.Where(n => n.Channel == c);
        if (!string.IsNullOrWhiteSpace(category))
            query = query.Where(n => n.Category == category);
        if (from is { } f)
            query = query.Where(n => n.CreatedAt >= f);
        if (to is { } t)
            query = query.Where(n => n.CreatedAt <= t);

        return await query
            .OrderByDescending(n => n.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(ct);
    }
}
