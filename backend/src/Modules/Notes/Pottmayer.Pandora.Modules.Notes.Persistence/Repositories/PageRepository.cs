using Microsoft.EntityFrameworkCore;
using Pottmayer.Pandora.Modules.Notes.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Notes.Domain.Ports.Repositories;
using Pottmayer.Tars.Data.Abstractions.DataContext;
using Pottmayer.Tars.Data.Relational.Repositories;

namespace Pottmayer.Pandora.Modules.Notes.Persistence.Repositories;

public sealed class PageRepository(IDataContextAccessor accessor)
    : StandardRepository<Page, Guid>(accessor), IPageRepository
{
    public Task<Page?> FindByIdForUserAsync(Guid id, Guid userId, CancellationToken ct = default)
        => Queryable().FirstOrDefaultAsync(
            p => p.Id == id && p.UserId == userId && p.DeletedAt == null, ct);

    public async Task<IReadOnlyList<Page>> GetTreeForUserAsync(
        Guid userId, bool includeArchived, CancellationToken ct = default)
    {
        var query = Queryable().Where(p => p.UserId == userId && p.DeletedAt == null);

        if (!includeArchived)
            query = query.Where(p => p.ArchivedAt == null);

        // OrderIndex first, then the time-ordered v7 id, so siblings sharing an index stay stable.
        return await query
            .OrderBy(p => p.OrderIndex)
            .ThenBy(p => p.Id)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyDictionary<Guid, Guid?>> GetParentMapForUserAsync(
        Guid userId, CancellationToken ct = default)
    {
        var rows = await Queryable()
            .Where(p => p.UserId == userId && p.DeletedAt == null)
            .Select(p => new { p.Id, p.ParentId })
            .ToListAsync(ct);

        return rows.ToDictionary(r => r.Id, r => r.ParentId);
    }

    public Task<bool> ExistsWithSlugAsync(Guid userId, string slug, CancellationToken ct = default)
        => Queryable().AnyAsync(
            p => p.UserId == userId && p.Slug == slug && p.DeletedAt == null, ct);
}
