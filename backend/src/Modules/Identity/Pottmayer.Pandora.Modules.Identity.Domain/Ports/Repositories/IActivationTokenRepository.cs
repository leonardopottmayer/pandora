using Pottmayer.Pandora.Modules.Identity.Domain.Entities;
using Pottmayer.Tars.Data.Abstractions.Repositories;

namespace Pottmayer.Pandora.Modules.Identity.Domain.Ports.Repositories;

public interface IActivationTokenRepository : IStandardRepository<AccountActivationToken, Guid>
{
    Task<AccountActivationToken?> FindByTokenHashAsync(string tokenHash, CancellationToken ct = default);
}
