using Microsoft.EntityFrameworkCore;
using Pottmayer.Pandora.Modules.Admin.Domain.Entities;
using Pottmayer.Pandora.Modules.Admin.Domain.Ports.Repositories;
using Pottmayer.Tars.Data.Abstractions.DataContext;
using Pottmayer.Tars.Data.Relational.Repositories;

namespace Pottmayer.Pandora.Modules.Admin.Persistence.Repositories;

public sealed class FeatureFlagOverrideRepository(IDataContextAccessor accessor)
    : StandardRepository<FeatureFlagOverride, string>(accessor), IFeatureFlagOverrideRepository
{
    public async Task<IReadOnlyList<FeatureFlagOverride>> ListAsync(CancellationToken ct = default)
        => await Queryable().AsNoTracking().ToListAsync(ct);
}
