using Pottmayer.Pandora.Modules.Notes.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Notes.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Notes.Domain.ValueObjects;

namespace Pottmayer.Pandora.Modules.Notes.Application.Services;

/// <summary>
/// Keeps the <see cref="PageLink"/> graph in sync with page content. Edges are derived data: a save
/// recomputes the whole outgoing set from the blocks and reconciles it against what is stored, so
/// saving the same blocks twice changes nothing and a removed page link drops its edge.
/// </summary>
internal static class PageLinkSynchronizer
{
    /// <summary>
    /// Rewrites the edges whose source is <paramref name="page"/>: a page named in the text is a
    /// <see cref="PageLinkKind.Wikilink"/>, a page-link block an <see cref="PageLinkKind.Embed"/>. Ids that
    /// are not the owner's live pages produce no edge.
    /// </summary>
    public static async Task RebuildAsync(
        Page page,
        PageContent content,
        IPageRepository pages,
        IPageLinkRepository links,
        TimeProvider timeProvider,
        CancellationToken ct)
    {
        var desired = await ResolveEdgesAsync(page, content, pages, ct);
        var existing = await links.GetBySourceAsync(page.Id, ct);

        var stale = existing.Where(l => !desired.Contains((l.TargetPageId, l.Kind.Value))).ToList();
        if (stale.Count > 0)
            await links.RemoveRangeAsync(stale, ct);

        // Only genuinely new edges are inserted; re-saving unchanged blocks touches nothing (and never
        // deletes then re-inserts the same row, which the unique index would reject mid-transaction).
        var kept = existing.Select(l => (l.TargetPageId, l.Kind.Value)).ToHashSet();
        foreach (var (targetId, kind) in desired.Where(e => !kept.Contains(e)))
            await links.AddAsync(
                PageLink.Create(page.Id, targetId, PageLinkKind.FromValue(kind), timeProvider), ct);
    }

    /// <summary>The edges the page's blocks ask for, as (target id, kind) pairs.</summary>
    private static async Task<HashSet<(Guid TargetId, string Kind)>> ResolveEdgesAsync(
        Page page, PageContent content, IPageRepository pages, CancellationToken ct)
    {
        var ids = content.MentionedPageIds.Concat(content.LinkedPageIds).ToHashSet();
        if (ids.Count == 0)
            return [];

        var live = (await pages.GetByIdsForUserAsync(ids, page.UserId, ct)).Select(p => p.Id).ToHashSet();

        var edges = new HashSet<(Guid, string)>();
        foreach (var id in content.MentionedPageIds.Where(live.Contains))
            edges.Add((id, PageLinkKind.Wikilink.Value));
        foreach (var id in content.LinkedPageIds.Where(live.Contains))
            edges.Add((id, PageLinkKind.Embed.Value));
        return edges;
    }
}
