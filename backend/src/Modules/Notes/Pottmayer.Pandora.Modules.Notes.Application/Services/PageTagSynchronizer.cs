using Pottmayer.Pandora.Modules.Notes.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Notes.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Notes.Domain.ValueObjects;

namespace Pottmayer.Pandora.Modules.Notes.Application.Services;

/// <summary>
/// Keeps a page's tags in sync with its content, the same way <see cref="PageLinkSynchronizer"/>
/// keeps the wiki edges: tags are derived data, so a save recomputes the whole set from the markdown
/// and reconciles it against what is stored.
///
/// Two things set it apart from the link graph. A tag that no page mentions yet is <em>created</em>
/// here (there is no CRUD that could have created it), and a tag whose last page just dropped it is
/// <em>deleted</em> here — unless it carries a color, which is the one thing the text cannot
/// remember and therefore worth keeping the row alive for.
/// </summary>
internal static class PageTagSynchronizer
{
    /// <summary>
    /// Rewrites the tags of <paramref name="page"/> from its markdown and returns them — the caller
    /// has just computed the page's tag list, so it does not have to read it back.
    /// </summary>
    public static async Task<IReadOnlyList<Tag>> RebuildAsync(
        Page page,
        ITagRepository tags,
        IPageTagRepository pageTags,
        TimeProvider timeProvider,
        CancellationToken ct)
    {
        var desired = await ResolveTagsAsync(page, tags, timeProvider, ct);
        var desiredIds = desired.Select(t => t.Id).ToHashSet();
        var existing = await pageTags.GetByPageAsync(page.Id, ct);

        var stale = existing.Where(t => !desiredIds.Contains(t.TagId)).ToList();
        if (stale.Count > 0)
            await pageTags.RemoveRangeAsync(stale, ct);

        // Only genuinely new rows are inserted; re-saving unchanged text touches nothing (and never
        // deletes then re-inserts the same row, which the unique index would reject mid-transaction).
        var kept = existing.Select(t => t.TagId).ToHashSet();
        foreach (var tagId in desiredIds.Where(id => !kept.Contains(id)))
            await pageTags.AddAsync(PageTag.Create(page.Id, tagId, timeProvider), ct);

        await SweepOrphansAsync(
            [.. stale.Select(t => t.TagId)], page.Id, page.UserId, tags, pageTags, ct);

        return desired;
    }

    /// <summary>
    /// Drops every tag of a page and sweeps whatever that orphaned. Runs when the page is deleted.
    /// </summary>
    public static async Task ClearAsync(
        Guid pageId,
        Guid userId,
        ITagRepository tags,
        IPageTagRepository pageTags,
        CancellationToken ct)
    {
        var rows = await pageTags.GetByPageAsync(pageId, ct);
        if (rows.Count == 0)
            return;

        await pageTags.RemoveRangeAsync(rows, ct);
        await SweepOrphansAsync([.. rows.Select(r => r.TagId)], pageId, userId, tags, pageTags, ct);
    }

    /// <summary>
    /// The tags the markdown asks for — creating the ones the user does not have yet.
    /// </summary>
    private static async Task<IReadOnlyList<Tag>> ResolveTagsAsync(
        Page page, ITagRepository tags, TimeProvider timeProvider, CancellationToken ct)
    {
        var references = TagParser.Parse(page.ContentMarkdown);
        if (references.Count == 0)
            return [];

        var slugs = references.Select(r => r.Slug).ToList();
        var known = (await tags.FindBySlugsAsync(page.UserId, slugs, ct))
            .ToDictionary(t => t.Slug);

        var resolved = new List<Tag>(references.Count);
        foreach (var reference in references)
        {
            if (!known.TryGetValue(reference.Slug, out var tag))
            {
                // First sighting: the tag exists from now on, named as it was written here.
                tag = Tag.Create(page.UserId, reference.Slug, reference.Name, timeProvider);
                await tags.AddAsync(tag, ct);
                known[reference.Slug] = tag;
            }

            resolved.Add(tag);
        }

        return resolved;
    }

    /// <summary>
    /// Deletes the tags among <paramref name="candidateIds"/> that no page mentions anymore and that
    /// hold nothing the text could not rebuild. Rows just removed in this transaction are discounted:
    /// the repository read still sees them.
    /// </summary>
    private static async Task SweepOrphansAsync(
        IReadOnlyCollection<Guid> candidateIds,
        Guid excludedPageId,
        Guid userId,
        ITagRepository tags,
        IPageTagRepository pageTags,
        CancellationToken ct)
    {
        if (candidateIds.Count == 0)
            return;

        var stillUsed = (await pageTags.GetByTagsAsync(candidateIds, ct))
            .Where(t => t.PageId != excludedPageId)
            .Select(t => t.TagId)
            .ToHashSet();

        var orphans = (await tags.GetByIdsForUserAsync(candidateIds, userId, ct))
            .Where(t => !stillUsed.Contains(t.Id) && !t.HasUserMetadata)
            .ToList();

        if (orphans.Count > 0)
            await tags.RemoveRangeAsync(orphans, ct);
    }
}
