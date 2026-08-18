using Microsoft.EntityFrameworkCore;
using Pottmayer.Pandora.Modules.Channels.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Channels.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Channels.Domain.ValueObjects;
using Pottmayer.Tars.Data.Abstractions.DataContext;
using Pottmayer.Tars.Data.Relational.Repositories;

namespace Pottmayer.Pandora.Modules.Channels.Persistence.Repositories;

public sealed class UserChannelRepository(IDataContextAccessor accessor)
    : StandardRepository<UserChannel, Guid>(accessor), IUserChannelRepository
{
    public Task<UserChannel?> FindAsync(Guid userId, Channel channel, CancellationToken ct = default) =>
        Queryable().FirstOrDefaultAsync(c => c.UserId == userId && c.Channel == channel, ct);

    public async Task<IReadOnlyList<UserChannel>> GetByUserAsync(Guid userId, CancellationToken ct = default) =>
        await Queryable()
            .Where(c => c.UserId == userId)
            .OrderBy(c => c.CreatedAt)
            .ToListAsync(ct);

    public Task<UserChannel?> FindByAddressAsync(Channel channel, NotificationAddress address, CancellationToken ct = default) =>
        Queryable().FirstOrDefaultAsync(c => c.Channel == channel && c.Address == address, ct);
}
