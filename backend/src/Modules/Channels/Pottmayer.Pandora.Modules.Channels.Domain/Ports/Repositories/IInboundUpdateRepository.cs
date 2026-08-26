using Pottmayer.Pandora.Modules.Channels.Domain.Aggregates;
using Pottmayer.Tars.Data.Relational.Abstractions.Repositories;

namespace Pottmayer.Pandora.Modules.Channels.Domain.Ports.Repositories;

public interface IInboundUpdateRepository : IStandardRepository<InboundUpdate, Guid>
{
    /// <summary>Whether this update was already recorded. The idempotency guard for a replayed poll.</summary>
    Task<bool> ExistsAsync(string provider, long providerUpdateId, CancellationToken ct = default);

    /// <summary>
    /// The highest update id seen for a provider, or null when none. Restores the long-polling offset
    /// after a restart, so a queued backlog is not re-delivered from the start.
    /// </summary>
    Task<long?> GetLastUpdateIdAsync(string provider, CancellationToken ct = default);

    /// <summary>
    /// Clears the raw payload (to null) of updates received before <paramref name="receivedBefore"/>
    /// whose raw is still present, leaving the rows themselves intact. Returns the number cleared.
    /// </summary>
    Task<int> PurgeRawOlderThanAsync(DateTimeOffset receivedBefore, CancellationToken ct = default);
}
