using Pottmayer.Pandora.Modules.Notes.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Notes.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Notes.Domain.ValueObjects;

namespace Pottmayer.Pandora.Modules.Notes.Application.Services;

/// <summary>
/// Keeps <see cref="PageMedia"/> in step with a page's markdown, the way <see cref="PageLinkSynchronizer"/>
/// keeps the wiki graph: a save works out the whole set again and reconciles it, so saving the same
/// text twice changes nothing and a removed image drops its row.
/// </summary>
internal static class PageMediaSynchronizer
{
    /// <summary>
    /// Rewrites the rows of <paramref name="page"/>. A path to an item that is not the owner's, or
    /// does not exist, produces no row — it only lives in the text.
    /// </summary>
    public static async Task RebuildAsync(
        Page page, IMediaItemRepository media, IPageMediaRepository pageMedia, TimeProvider timeProvider, CancellationToken ct)
    {
        var referenced = MediaReferenceParser.Parse(page.ContentMarkdown);
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
