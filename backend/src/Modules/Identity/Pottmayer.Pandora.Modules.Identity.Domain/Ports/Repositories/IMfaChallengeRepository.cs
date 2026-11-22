using Pottmayer.Pandora.Modules.Identity.Domain.Entities;
using Pottmayer.Tars.Data.Abstractions.Repositories;

namespace Pottmayer.Pandora.Modules.Identity.Domain.Ports.Repositories;

public interface IMfaChallengeRepository : IStandardRepository<MfaChallenge, Guid>
{
    Task<MfaChallenge?> FindByTokenHashAsync(string tokenHash, CancellationToken ct = default);
}
