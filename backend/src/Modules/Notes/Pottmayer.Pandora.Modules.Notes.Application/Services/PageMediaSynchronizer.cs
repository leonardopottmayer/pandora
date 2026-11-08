using Pottmayer.Pandora.Modules.Notes.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Notes.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Notes.Domain.ValueObjects;

namespace Pottmayer.Pandora.Modules.Notes.Application.Services;

/// <summary>
/// Keeps <see cref="PageMedia"/> in step with a page's blocks, the way <see cref="PageLinkSynchronizer"/>
/// keeps the wiki graph: a save works out the whole set again and reconciles it, so saving the same
/// text twice changes nothing and a removed image drops its row.
/// </summary>
internal static class PageMediaSynchronizer
{
    /// <summary>
    /// Rewrites the rows of <paramref name="page"/>, from its blocks and its cover. An id of an item that is not the owner's, or
    /// does not exist, produces no row.
    /// </summary>
    public static async Task RebuildAsync(
        Page page, PageContent content, IMediaItemRepository media, IPageMediaRepository pageMedia, TimeProvider timeProvider, CancellationToken ct)
    {
        // The blocks' items, and the cover's: a cover from the library is a use of it too.
        IReadOnlyCollection<Guid> referenced = PageAppearance.Parse(page.Appearance).CoverMediaId is { } cover
            ? [.. content.MediaIds.Append(cover).Distinct()]
            : content.MediaIds;
        var owned = referenced.Count == 0
            ? []
            : (await media.GetByIdsForUserAsync(referenced, page.UserId, ct)).Select(m => m.Id).ToHashSet();

        var existing = await pageMedia.GetByPageAsync(page.Id, ct);
        var stale = existing.Where(r => !owned.Contains(r.MediaId)).ToList();
        if (stale.Count > 0)
            await pageMedia.RemoveRangeAsync(stale, ct);

        // Only new rows are inserted, never delete-then-insert, which the unique index would reject.
        var kept = existing.Select(r => r.MediaId).ToHashSet();
        foreach (var mediaId in owned.Where(id => !kept.Contains(id)))
            await pageMedia.AddAsync(PageMedia.Create(page.Id, mediaId, timeProvider), ct);
    }
}
