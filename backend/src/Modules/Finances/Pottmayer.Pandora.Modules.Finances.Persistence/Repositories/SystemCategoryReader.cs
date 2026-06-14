using Microsoft.EntityFrameworkCore;
using Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Finances.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Finances.Domain.ValueObjects;
using Pottmayer.Tars.Data.Abstractions.DataContext;
using Pottmayer.Tars.Data.Relational.Repositories;

namespace Pottmayer.Pandora.Modules.Finances.Persistence.Repositories;

public sealed class SystemCategoryReader(IDataContextAccessor accessor)
    : StandardRepository<SystemCategory, Guid>(accessor), ISystemCategoryReader
{
    public async Task<IReadOnlyList<SystemCategory>> GetAllAsync(
        string? nature, bool includeInactive, CancellationToken ct = default)
    {
        var query = Queryable();

        if (!includeInactive)
            query = query.Where(c => c.IsActive);

        if (!string.IsNullOrWhiteSpace(nature))
        {
            var natureVo = TransactionNature.FromValue(nature);
            query = query.Where(c => c.Nature == natureVo);
        }

        return await query
            .OrderBy(c => c.DisplayOrder)
            .ThenBy(c => c.Name)
            .ToListAsync(ct);
    }

    public Task<SystemCategory?> GetByCodeAsync(string code, CancellationToken ct = default) =>
        Queryable().FirstOrDefaultAsync(c => c.Code == code, ct);
}
