using Microsoft.EntityFrameworkCore;
using Pottmayer.Pandora.Modules.Identity.Domain.Entities;
using Pottmayer.Pandora.Modules.Identity.Domain.Ports.Repositories;
using Pottmayer.Tars.Data.Abstractions.DataContext;
using Pottmayer.Tars.Data.Relational.Repositories;

namespace Pottmayer.Pandora.Modules.Identity.Persistence.Repositories;

public sealed class MfaChallengeRepository(IDataContextAccessor accessor)
    : StandardRepository<MfaChallenge, Guid>(accessor), IMfaChallengeRepository
{
    public Task<MfaChallenge?> FindByTokenHashAsync(string tokenHash, CancellationToken ct = default)
        => Queryable().FirstOrDefaultAsync(c => c.TokenHash == tokenHash, ct);
}
