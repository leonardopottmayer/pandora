using Microsoft.EntityFrameworkCore;
using Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Finances.Domain.Ports.Repositories;
using Pottmayer.Tars.Data.Abstractions.DataContext;
using Pottmayer.Tars.Data.Relational.Repositories;

namespace Pottmayer.Pandora.Modules.Finances.Persistence.Repositories;

public sealed class SystemAssetClassRepository(IDataContextAccessor accessor)
    : StandardRepository<SystemAssetClass, Guid>(accessor), ISystemAssetClassRepository
{
    public Task<SystemAssetClass?> FindAsync(Guid id, CancellationToken ct = default)
        => Queryable().FirstOrDefaultAsync(c => c.Id == id, ct);

    public async Task<IReadOnlyList<SystemAssetClass>> ListAsync(CancellationToken ct = default)
        => await Queryable().OrderBy(c => c.DisplayOrder).ToListAsync(ct);
}
