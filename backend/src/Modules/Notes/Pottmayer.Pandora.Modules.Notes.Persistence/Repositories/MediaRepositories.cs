using Microsoft.EntityFrameworkCore;
using Pottmayer.Pandora.Modules.Notes.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Notes.Domain.Ports.Repositories;
using Pottmayer.Tars.Data.Abstractions.DataContext;
using Pottmayer.Tars.Data.Relational.Repositories;

namespace Pottmayer.Pandora.Modules.Notes.Persistence.Repositories;

public sealed class MediaItemRepository(IDataContextAccessor accessor)
    : StandardRepository<MediaItem, Guid>(accessor), IMediaItemRepository
{
    private IQueryable<MediaItem> Live(Guid userId) =>
        Queryable().Where(m => m.UserId == userId && m.DeletedAt == null);

    public Task<MediaItem?> FindByIdForUserAsync(Guid id, Guid userId, CancellationToken ct = default)
        => Live(userId).FirstOrDefaultAsync(m => m.Id == id, ct);

    public async Task<IReadOnlyList<MediaItem>> ListAsync(
        Guid userId, Guid? folderId, bool unsortedOnly, string? kind, string? search, CancellationToken ct = default)
    {
        var query = Live(userId);
        if (folderId is { } folder)
            query = query.Where(m => m.FolderId == folder);
        else if (unsortedOnly)
            query = query.Where(m => m.FolderId == null);
        if (!string.IsNullOrWhiteSpace(kind))
            query = query.Where(m => m.Kind == kind);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search.Trim()}%";
            query = query.Where(m => EF.Functions.ILike(m.Name, pattern));
        }

        // The time-ordered v7 id is the upload order.
        return await query.OrderByDescending(m => m.Id).ToListAsync(ct);
    }

    public async Task<MediaCounts> CountAsync(Guid userId, CancellationToken ct = default)
    {
        var rows = await Live(userId)
            .GroupBy(m => m.FolderId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToListAsync(ct);

        return new MediaCounts(
            rows.Where(r => r.Key != null).ToDictionary(r => r.Key!.Value, r => r.Count),
            rows.Where(r => r.Key == null).Sum(r => r.Count),
            rows.Sum(r => r.Count));
    }

    public async Task<IReadOnlyList<MediaItem>> GetInFolderAsync(Guid userId, Guid folderId, CancellationToken ct = default)
        => await Live(userId).Where(m => m.FolderId == folderId).ToListAsync(ct);

    public async Task<IReadOnlyList<MediaItem>> GetByIdsForUserAsync(
        IReadOnlyCollection<Guid> ids, Guid userId, CancellationToken ct = default)
    {
        if (ids.Count == 0)
            return [];
        return await Queryable().Where(m => m.UserId == userId && ids.Contains(m.Id)).ToListAsync(ct);
    }
}

public sealed class PageMediaRepository(IDataContextAccessor accessor)
    : StandardRepository<PageMedia, Guid>(accessor), IPageMediaRepository
{
    public async Task<IReadOnlyList<PageMedia>> GetByPageAsync(Guid pageId, CancellationToken ct = default)
        => await Queryable().Where(r => r.PageId == pageId).ToListAsync(ct);

    public async Task<IReadOnlyList<Guid>> GetPageIdsByMediaAsync(Guid mediaId, CancellationToken ct = default)
        => await Queryable().Where(r => r.MediaId == mediaId).Select(r => r.PageId).ToListAsync(ct);

    public async Task<IReadOnlyDictionary<Guid, int>> CountByMediaAsync(
        IReadOnlyCollection<Guid> mediaIds, CancellationToken ct = default)
    {
        if (mediaIds.Count == 0)
            return new Dictionary<Guid, int>();

        var rows = await Queryable()
            .Where(r => mediaIds.Contains(r.MediaId))
            .GroupBy(r => r.MediaId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToListAsync(ct);
        return rows.ToDictionary(r => r.Key, r => r.Count);
    }

    public async Task RemoveByPageAsync(Guid pageId, CancellationToken ct = default)
    {
        var rows = await Queryable().Where(r => r.PageId == pageId).ToListAsync(ct);
        if (rows.Count > 0)
            await RemoveRangeAsync(rows, ct);
    }
}

public sealed class MediaFolderRepository(IDataContextAccessor accessor)
    : StandardRepository<MediaFolder, Guid>(accessor), IMediaFolderRepository
{
    public Task<MediaFolder?> FindByIdForUserAsync(Guid id, Guid userId, CancellationToken ct = default)
        => Queryable().FirstOrDefaultAsync(f => f.Id == id && f.UserId == userId, ct);

    public async Task<IReadOnlyList<MediaFolder>> GetForUserAsync(Guid userId, CancellationToken ct = default)
        => await Queryable().Where(f => f.UserId == userId).OrderBy(f => f.Name).ToListAsync(ct);
}

public sealed class MediaRenditionRepository(IDataContextAccessor accessor)
    : StandardRepository<MediaRendition, Guid>(accessor), IMediaRenditionRepository
{
    public Task<MediaRendition?> FindAsync(Guid mediaId, string purpose, CancellationToken ct = default)
        => Queryable().FirstOrDefaultAsync(r => r.MediaId == mediaId && r.Purpose == purpose, ct);

    public async Task<IReadOnlySet<Guid>> WithPurposeAsync(
        IReadOnlyCollection<Guid> mediaIds, string purpose, CancellationToken ct = default)
    {
        if (mediaIds.Count == 0)
            return new HashSet<Guid>();

        var ids = await Queryable()
            .Where(r => mediaIds.Contains(r.MediaId) && r.Purpose == purpose)
            .Select(r => r.MediaId)
            .ToListAsync(ct);
        return ids.ToHashSet();
    }
}
