using Microsoft.EntityFrameworkCore;
using Pottmayer.Pandora.Modules.Channels.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Channels.Domain.Ports.Repositories;
using Pottmayer.Tars.Data.Abstractions.DataContext;
using Pottmayer.Tars.Data.Relational.Repositories;

namespace Pottmayer.Pandora.Modules.Channels.Persistence.Repositories;

public sealed class ChannelLinkTokenRepository(IDataContextAccessor accessor)
    : StandardRepository<ChannelLinkToken, Guid>(accessor), IChannelLinkTokenRepository
{
    public Task<ChannelLinkToken?> FindByHashAsync(string tokenHash, CancellationToken ct = default) =>
        Queryable().FirstOrDefaultAsync(t => t.TokenHash == tokenHash, ct);
}
