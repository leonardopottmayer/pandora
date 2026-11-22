using Microsoft.EntityFrameworkCore;
using NpgsqlTypes;
using Pottmayer.Pandora.Modules.Notes.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Notes.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Notes.Persistence.EntityConfigs;
using Pottmayer.Tars.Data.Abstractions.DataContext;
using Pottmayer.Tars.Data.Relational.Repositories;

namespace Pottmayer.Pandora.Modules.Notes.Persistence.Repositories;

public sealed class PageRepository(IDataContextAccessor accessor)
    : StandardRepository<Page, Guid>(accessor), IPageRepository
{
    public async Task<IReadOnlyList<Page>> GetInFolderAsync(Guid userId, Guid folderId, bool includeArchived,
        IReadOnlyCollection<Guid>? pageIds = null, CancellationToken ct = default)
    {
        if (pageIds is { Count: 0 }) return [];
        return await Queryable().AsNoTracking()
            .Where(page => page.UserId == userId && page.FolderId == folderId && page.ParentId == null && page.DeletedAt == null &&
                (includeArchived || page.ArchivedAt == null) && (pageIds == null || pageIds.Contains(page.Id)))
            .OrderBy(page => page.OrderIndex).ThenBy(page => page.Id).ToListAsync(ct);
    }

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

    public async Task<IReadOnlyList<Page>> GetByIdsForUserAsync(
        IReadOnlyCollection<Guid> ids, Guid userId, CancellationToken ct = default)
    {
        if (ids.Count == 0)
            return [];

        return await Queryable()
            .Where(p => ids.Contains(p.Id) && p.UserId == userId && p.DeletedAt == null)
            .OrderBy(p => p.Title)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyDictionary<Guid, SpacePageStats>> GetSpaceStatsAsync(
        Guid userId, CancellationToken ct = default)
    {
        var rows = await Queryable()
            .Where(p => p.UserId == userId && p.DeletedAt == null)
            .GroupBy(p => p.SpaceId)
            .Select(g => new { g.Key, Count = g.Count(), Last = g.Max(p => p.UpdatedAt ?? p.CreatedAt) })
            .ToListAsync(ct);

        return rows.ToDictionary(r => r.Key, r => new SpacePageStats(r.Count, r.Last));
    }

    public async Task<IReadOnlyList<Page>> GetRecentForUserAsync(Guid userId, int limit, CancellationToken ct = default)
        => await Queryable()
            .Where(p => p.UserId == userId && p.DeletedAt == null && p.ViewedAt != null)
            .OrderByDescending(p => p.ViewedAt)
            .Take(limit)
            .ToListAsync(ct);

    public async Task<bool> MarkViewedAsync(Guid id, Guid userId, DateTimeOffset at, CancellationToken ct = default)
        => await Queryable()
            .Where(p => p.Id == id && p.UserId == userId && p.DeletedAt == null)
            .ExecuteUpdateAsync(set => set.SetProperty(p => p.ViewedAt, at), ct) > 0;

    public async Task<IReadOnlyList<Page>> GetDeletedForUserAsync(Guid userId, CancellationToken ct = default)
        => await Queryable()
            .Where(p => p.UserId == userId && p.DeletedAt != null)
            .OrderByDescending(p => p.DeletedAt)
            .ThenBy(p => p.Id)
            .ToListAsync(ct);

    public Task PurgeAsync(Guid id, Guid userId, CancellationToken ct = default)
        => Queryable().Where(p => p.Id == id && p.UserId == userId && p.DeletedAt != null).ExecuteDeleteAsync(ct);

    public Task<Page?> FindByPublicTokenAsync(string token, CancellationToken ct = default)
        => Queryable().AsNoTracking().FirstOrDefaultAsync(p => p.PublicToken == token && p.DeletedAt == null, ct);

    public async Task<IReadOnlyList<Page>> SearchAsync(
        Guid userId, string tsQuery, int limit, Guid? spaceId = null,
        IReadOnlyCollection<Guid>? pageIds = null, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(tsQuery) || pageIds is { Count: 0 })
            return [];

        // Matches against the stored tsvector, so the GIN index does the work. The configuration
        // ('simple') is the one the generated column was built with — they have to agree. Pages whose
        // title matches come first: searching "ideias" means the "Ideias…" page before one that only
        // mentions it.
        var query = Queryable()
            .Where(p => p.UserId == userId && p.DeletedAt == null &&
                        (spaceId == null || p.SpaceId == spaceId) &&
                        EF.Property<NpgsqlTsVector>(p, PageColumns.SearchVector)
                          .Matches(EF.Functions.ToTsQuery("simple", tsQuery)));

        if (pageIds is not null)
            query = query.Where(p => pageIds.Contains(p.Id));

        // Cover-density ranking rewards nearby matches as well as repeated occurrences. Filter
        // before ranking/limiting so a tagged hit cannot disappear behind unrelated top results.
        return await query
            .OrderByDescending(p => EF.Functions.ToTsVector("simple", p.Title).Matches(EF.Functions.ToTsQuery("simple", tsQuery)))
            .ThenByDescending(p => EF.Property<NpgsqlTsVector>(p, PageColumns.SearchVector)
                .RankCoverDensity(EF.Functions.ToTsQuery("simple", tsQuery)))
            .ThenBy(p => p.Title)
            .ThenBy(p => p.Id)
            .Take(limit)
            .ToListAsync(ct);
    }
}
