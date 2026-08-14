using Microsoft.EntityFrameworkCore;
using Pottmayer.Pandora.Modules.Notes.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Notes.Domain.Ports.Repositories;
using Pottmayer.Tars.Data.Abstractions.DataContext;
using Pottmayer.Tars.Data.Relational.Repositories;

namespace Pottmayer.Pandora.Modules.Notes.Persistence.Repositories;

public sealed class PageLinkRepository(IDataContextAccessor accessor)
    : StandardRepository<PageLink, Guid>(accessor), IPageLinkRepository
{
    public async Task<IReadOnlyList<PageLink>> GetBySourceAsync(
        Guid sourcePageId, CancellationToken ct = default)
        => await Queryable().Where(l => l.SourcePageId == sourcePageId).ToListAsync(ct);

    public async Task<IReadOnlyList<PageLink>> GetByTargetAsync(
        Guid targetPageId, CancellationToken ct = default)
        => await Queryable().Where(l => l.TargetPageId == targetPageId).ToListAsync(ct);

    public async Task RemoveBySourceAsync(Guid sourcePageId, CancellationToken ct = default)
    {
        var links = await Queryable().Where(l => l.SourcePageId == sourcePageId).ToListAsync(ct);
        if (links.Count > 0)
            await RemoveRangeAsync(links, ct);
    }
}
