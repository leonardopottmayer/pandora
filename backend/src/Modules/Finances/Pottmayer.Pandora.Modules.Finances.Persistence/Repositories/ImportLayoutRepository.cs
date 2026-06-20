using Microsoft.EntityFrameworkCore;
using Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Finances.Domain.Ports.Repositories;
using Pottmayer.Tars.Data.Abstractions.DataContext;
using Pottmayer.Tars.Data.Relational.Repositories;

namespace Pottmayer.Pandora.Modules.Finances.Persistence.Repositories;

public sealed class ImportLayoutRepository(IDataContextAccessor accessor)
    : StandardRepository<ImportLayout, Guid>(accessor), IImportLayoutRepository
{
    public Task<ImportLayout?> FindByCodeAsync(string layoutCode, CancellationToken ct = default)
        => Queryable().FirstOrDefaultAsync(l => l.LayoutCode == layoutCode && l.UserId == null, ct);

    public async Task<IReadOnlyList<ImportLayout>> GetSystemLayoutsAsync(CancellationToken ct = default)
        => await Queryable().Where(l => l.UserId == null).OrderBy(l => l.Name).ToListAsync(ct);
}
