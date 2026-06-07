using Microsoft.EntityFrameworkCore;
using Pottmayer.Pandora.Modules.Identity.Domain.Entities;
using Pottmayer.Pandora.Modules.Identity.Domain.Ports.Repositories;
using Pottmayer.Tars.Data.Abstractions.DataContext;
using Pottmayer.Tars.Data.Relational.Repositories;

namespace Pottmayer.Pandora.Modules.Identity.Persistence.Repositories;

public sealed class PasswordResetTokenRepository(IDataContextAccessor accessor)
    : StandardRepository<PasswordResetToken, Guid>(accessor), IPasswordResetTokenRepository
{
    public Task<PasswordResetToken?> FindByTokenHashAsync(string tokenHash, CancellationToken ct = default)
        => Queryable().FirstOrDefaultAsync(t => t.TokenHash == tokenHash, ct);
}
