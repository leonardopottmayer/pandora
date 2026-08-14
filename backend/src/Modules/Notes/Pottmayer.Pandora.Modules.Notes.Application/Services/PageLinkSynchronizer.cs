using Pottmayer.Pandora.Modules.Notes.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Notes.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Notes.Domain.ValueObjects;

namespace Pottmayer.Pandora.Modules.Notes.Application.Services;

/// <summary>
/// Keeps the <see cref="PageLink"/> graph in sync with page content. Edges are derived data: a save
/// recomputes the whole outgoing set from the markdown and reconciles it against what is stored, so
/// saving the same text twice changes nothing and a removed wikilink drops its edge.
/// </summary>
internal static class PageLinkSynchronizer
{
    /// <summary>
    /// Rewrites the edges whose source is <paramref name="page"/>. References that match no page of
    /// the owner produce no edge — a "broken" link only exists in the text, never in the graph.
    /// </summary>
    public static async Task RebuildAsync(
        Page page,
        IPageRepository pages,
        IPageLinkRepository links,
        TimeProvider timeProvider,
        CancellationToken ct)
    {
        var desired = await ResolveEdgesAsync(page, pages, ct);
        var existing = await links.GetBySourceAsync(page.Id, ct);

        var stale = existing.Where(l => !desired.Contains((l.TargetPageId, l.Kind.Value))).ToList();
        if (stale.Count > 0)
            await links.RemoveRangeAsync(stale, ct);

        // Only genuinely new edges are inserted; re-saving unchanged text touches nothing (and never
        // deletes then re-inserts the same row, which the unique index would reject mid-transaction).
        var kept = existing.Select(l => (l.TargetPageId, l.Kind.Value)).ToHashSet();
        foreach (var (targetId, kind) in desired.Where(e => !kept.Contains(e)))
            await links.AddAsync(
                PageLink.Create(page.Id, targetId, PageLinkKind.FromValue(kind), timeProvider), ct);
    }

    /// <summary>The edges the page's markdown asks for, as (target id, kind) pairs.</summary>
    private static async Task<HashSet<(Guid TargetId, string Kind)>> ResolveEdgesAsync(
        Page page, IPageRepository pages, CancellationToken ct)
    {
        var references = WikilinkParser.Parse(page.ContentMarkdown);
        if (references.Count == 0)
            return [];

        // A reference may be written as the page's title ("[[My Notes]]") or as its slug
        // ("[[my-notes]]"), so both spellings are looked up in one round trip.
        var lowerTitles = references.Select(r => r.Target.ToLowerInvariant()).Distinct().ToList();
        var slugs = references.Select(r => Slugger.From(r.Target)).Distinct().ToList();

        var candidates = await pages.FindByTitlesOrSlugsAsync(page.UserId, lowerTitles, slugs, ct);
        if (candidates.Count == 0)
            return [];

        var byTitle = candidates
            .GroupBy(p => p.Title.ToLowerInvariant())
            .ToDictionary(g => g.Key, g => g.First().Id);
        var bySlug = candidates
            .GroupBy(p => p.Slug)
            .ToDictionary(g => g.Key, g => g.First().Id);

        // Two spellings of the same page ("[[My Notes]]" and "[[my-notes]]") collapse into one edge.
        var edges = new HashSet<(Guid, string)>();
        foreach (var reference in references)
        {
            if (Resolve(reference.Target, byTitle, bySlug, out var targetId))
                edges.Add((targetId, reference.Kind.Value));
        }

        return edges;
    }

    /// <summary>Title first (what the author most likely typed), then the slugified form.</summary>
    private static bool Resolve(
        string target,
        IReadOnlyDictionary<string, Guid> byTitle,
        IReadOnlyDictionary<string, Guid> bySlug,
        out Guid targetId)
        => byTitle.TryGetValue(target.ToLowerInvariant(), out targetId) ||
           bySlug.TryGetValue(Slugger.From(target), out targetId);
}
