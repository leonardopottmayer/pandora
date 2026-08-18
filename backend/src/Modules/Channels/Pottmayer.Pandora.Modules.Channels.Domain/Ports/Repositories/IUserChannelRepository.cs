using Pottmayer.Pandora.Modules.Channels.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Channels.Domain.ValueObjects;
using Pottmayer.Tars.Data.Relational.Abstractions.Repositories;

namespace Pottmayer.Pandora.Modules.Channels.Domain.Ports.Repositories;

public interface IUserChannelRepository : IStandardRepository<UserChannel, Guid>
{
    /// <summary>The user's address on one channel, linked or not.</summary>
    Task<UserChannel?> FindAsync(Guid userId, Channel channel, CancellationToken ct = default);

    /// <summary>Every channel the user has, for the settings screen.</summary>
    Task<IReadOnlyList<UserChannel>> GetByUserAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Resolves an inbound address back to its owner. Step zero of triage: an update from a chat with
    /// no row here belongs to nobody and goes no further.
    /// </summary>
    Task<UserChannel?> FindByAddressAsync(Channel channel, NotificationAddress address, CancellationToken ct = default);
}
