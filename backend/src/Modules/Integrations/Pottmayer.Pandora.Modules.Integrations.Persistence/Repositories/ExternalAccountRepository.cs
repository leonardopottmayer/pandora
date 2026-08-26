using Microsoft.EntityFrameworkCore;
using Pottmayer.Pandora.Modules.Integrations.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Integrations.Domain.Ports.Repositories;
using Pottmayer.Tars.Data.Abstractions.DataContext;
using Pottmayer.Tars.Data.Relational.Repositories;

namespace Pottmayer.Pandora.Modules.Integrations.Persistence.Repositories;

public sealed class ExternalAccountRepository(IDataContextAccessor accessor)
    : StandardRepository<ExternalAccount, Guid>(accessor), IExternalAccountRepository
{
    public Task<ExternalAccount?> FindAsync(Guid userId, string provider, CancellationToken ct = default) =>
        Queryable().FirstOrDefaultAsync(a => a.UserId == userId && a.Provider == provider, ct);

    public async Task<IReadOnlyList<ExternalAccount>> GetByUserAsync(Guid userId, CancellationToken ct = default) =>
        await Queryable()
            .Where(a => a.UserId == userId)
            .OrderBy(a => a.CreatedAt)
            .ToListAsync(ct);
}
