using Microsoft.EntityFrameworkCore;
using Pottmayer.Pandora.Modules.Notes.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Notes.Domain.Ports.Repositories;
using Pottmayer.Tars.Data.Abstractions.DataContext;
using Pottmayer.Tars.Data.Relational.Repositories;

namespace Pottmayer.Pandora.Modules.Notes.Persistence.Repositories;

public sealed class PageVersionRepository(IDataContextAccessor accessor)
    : StandardRepository<PageVersion, Guid>(accessor), IPageVersionRepository
{
    public async Task<IReadOnlyList<PageVersionInfo>> ListAsync(Guid pageId, Guid userId, long? before, CancellationToken ct)
        => await Queryable().AsNoTracking()
            .Where(v => v.PageId == pageId && v.UserId == userId && (before == null || v.Number < before))
            .OrderByDescending(v => v.Number).Take(100)
            .Select(v => new PageVersionInfo(v.Number, v.CreatedAt, v.Source, v.RestoredFrom)).ToListAsync(ct);

    public Task<PageVersion?> FindAsync(Guid pageId, Guid userId, long number, CancellationToken ct)
        => Queryable().AsNoTracking().FirstOrDefaultAsync(
            v => v.PageId == pageId && v.UserId == userId && v.Number == number, ct);

    public Task PurgeByPageAsync(Guid pageId, CancellationToken ct)
        => Queryable().Where(v => v.PageId == pageId).ExecuteDeleteAsync(ct);
}
