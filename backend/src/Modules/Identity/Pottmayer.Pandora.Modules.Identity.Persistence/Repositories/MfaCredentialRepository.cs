using Microsoft.EntityFrameworkCore;
using Pottmayer.Pandora.Modules.Identity.Domain.Entities;
using Pottmayer.Pandora.Modules.Identity.Domain.Ports.Repositories;
using Pottmayer.Tars.Data.Abstractions.DataContext;
using Pottmayer.Tars.Data.Relational.Repositories;

namespace Pottmayer.Pandora.Modules.Identity.Persistence.Repositories;

public sealed class MfaCredentialRepository(IDataContextAccessor accessor)
    : StandardRepository<MfaCredential, Guid>(accessor), IMfaCredentialRepository
{
    public Task<MfaCredential?> FindByUserIdAsync(Guid userId, CancellationToken ct = default)
        => Queryable().FirstOrDefaultAsync(c => c.UserId == userId, ct);

    public async Task RemoveByUserIdAsync(Guid userId, CancellationToken ct = default)
    {
        var existing = await Queryable().Where(c => c.UserId == userId).ToListAsync(ct);
        if (existing.Count > 0)
            await RemoveRangeAsync(existing, ct);
    }
}
