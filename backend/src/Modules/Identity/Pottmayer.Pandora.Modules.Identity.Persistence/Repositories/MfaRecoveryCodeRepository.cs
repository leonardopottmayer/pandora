using Microsoft.EntityFrameworkCore;
using Pottmayer.Pandora.Modules.Identity.Domain.Entities;
using Pottmayer.Pandora.Modules.Identity.Domain.Ports.Repositories;
using Pottmayer.Tars.Data.Abstractions.DataContext;
using Pottmayer.Tars.Data.Relational.Repositories;

namespace Pottmayer.Pandora.Modules.Identity.Persistence.Repositories;

public sealed class MfaRecoveryCodeRepository(IDataContextAccessor accessor)
    : StandardRepository<MfaRecoveryCode, Guid>(accessor), IMfaRecoveryCodeRepository
{
    public async Task<IReadOnlyList<MfaRecoveryCode>> ListByUserIdAsync(Guid userId, CancellationToken ct = default)
        => await Queryable().Where(c => c.UserId == userId).ToListAsync(ct);

    public Task<int> CountActiveByUserIdAsync(Guid userId, CancellationToken ct = default)
        => Queryable().CountAsync(c => c.UserId == userId && c.ConsumedAt == null, ct);

    public async Task RemoveAllForUserAsync(Guid userId, CancellationToken ct = default)
    {
        var existing = await Queryable().Where(c => c.UserId == userId).ToListAsync(ct);
        if (existing.Count > 0)
            await RemoveRangeAsync(existing, ct);
    }
}
