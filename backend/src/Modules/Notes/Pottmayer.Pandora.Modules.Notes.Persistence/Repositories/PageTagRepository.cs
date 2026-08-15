using Microsoft.EntityFrameworkCore;
using Pottmayer.Pandora.Modules.Notes.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Notes.Domain.Ports.Repositories;
using Pottmayer.Tars.Data.Abstractions.DataContext;
using Pottmayer.Tars.Data.Relational.Repositories;

namespace Pottmayer.Pandora.Modules.Notes.Persistence.Repositories;

public sealed class PageTagRepository(IDataContextAccessor accessor)
    : StandardRepository<PageTag, Guid>(accessor), IPageTagRepository
{
    public async Task<IReadOnlyList<PageTag>> GetByPageAsync(Guid pageId, CancellationToken ct = default)
        => await Queryable().Where(t => t.PageId == pageId).ToListAsync(ct);

    public async Task<IReadOnlyList<PageTag>> GetByPagesAsync(
        IReadOnlyCollection<Guid> pageIds, CancellationToken ct = default)
    {
        if (pageIds.Count == 0)
            return [];

        return await Queryable().Where(t => pageIds.Contains(t.PageId)).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<PageTag>> GetByTagsAsync(
        IReadOnlyCollection<Guid> tagIds, CancellationToken ct = default)
    {
        if (tagIds.Count == 0)
            return [];

        return await Queryable().Where(t => tagIds.Contains(t.TagId)).ToListAsync(ct);
    }

    public async Task RemoveByPageAsync(Guid pageId, CancellationToken ct = default)
    {
        var rows = await Queryable().Where(t => t.PageId == pageId).ToListAsync(ct);
        if (rows.Count > 0)
            await RemoveRangeAsync(rows, ct);
    }
}
