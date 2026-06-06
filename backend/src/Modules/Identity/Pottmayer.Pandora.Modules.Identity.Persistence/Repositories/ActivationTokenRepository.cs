using Microsoft.EntityFrameworkCore;
using Pottmayer.Pandora.Modules.Identity.Domain.Entities;
using Pottmayer.Pandora.Modules.Identity.Domain.Ports.Repositories;
using Pottmayer.Tars.Data.Abstractions.DataContext;
using Pottmayer.Tars.Data.Relational.Repositories;

namespace Pottmayer.Pandora.Modules.Identity.Persistence.Repositories;

public sealed class ActivationTokenRepository(IDataContextAccessor accessor)
    : StandardRepository<AccountActivationToken, Guid>(accessor), IActivationTokenRepository
{
    public Task<AccountActivationToken?> FindByTokenHashAsync(string tokenHash, CancellationToken ct = default)
        => Queryable().FirstOrDefaultAsync(t => t.TokenHash == tokenHash, ct);
}
