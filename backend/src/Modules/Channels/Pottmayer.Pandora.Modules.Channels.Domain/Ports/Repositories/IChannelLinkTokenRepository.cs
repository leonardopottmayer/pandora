using Pottmayer.Pandora.Modules.Channels.Domain.Aggregates;
using Pottmayer.Tars.Data.Relational.Abstractions.Repositories;

namespace Pottmayer.Pandora.Modules.Channels.Domain.Ports.Repositories;

public interface IChannelLinkTokenRepository : IStandardRepository<ChannelLinkToken, Guid>
{
    /// <summary>Looks a token up by its hash. Returns expired and consumed ones too, so the caller can say which.</summary>
    Task<ChannelLinkToken?> FindByHashAsync(string tokenHash, CancellationToken ct = default);
}
