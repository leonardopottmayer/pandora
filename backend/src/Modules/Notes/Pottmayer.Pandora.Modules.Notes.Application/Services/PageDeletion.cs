using Pottmayer.Pandora.Modules.Notes.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Notes.Domain.Ports.Repositories;
using Pottmayer.Tars.Data.Abstractions.DataContext;

namespace Pottmayer.Pandora.Modules.Notes.Application.Services;

/// <summary>Soft-deletes a page with its sub-pages, and what they derived: outgoing links, media use, tags.</summary>
internal static class PageDeletion
{
    public static async Task DeleteSubtreesAsync(
        IDataContext ctx, IReadOnlyCollection<Guid> rootIds, Guid userId, TimeProvider timeProvider, CancellationToken ct)
    {
        if (rootIds.Count == 0)
            return;

        var repo = ctx.AcquireRepository<IPageRepository>();
        var links = ctx.AcquireRepository<IPageLinkRepository>();
        var tags = ctx.AcquireRepository<ITagRepository>();
        var pageTags = ctx.AcquireRepository<IPageTagRepository>();
        var pageMedia = ctx.AcquireRepository<IPageMediaRepository>();

        var all = await repo.GetTreeForUserAsync(userId, includeArchived: true, ct);
        var now = timeProvider.GetUtcNow();
        foreach (var descendant in rootIds.SelectMany(id => PageHierarchy.Subtree(id, all)).DistinctBy(p => p.Id))
        {
            descendant.Delete(now);
            await repo.UpdateAsync(descendant, ct);

            // Edges leaving a deleted page are gone for good; edges pointing at it stay and are
            // filtered out on read, so restoring the row would restore its inbound mentions.
            await links.RemoveBySourceAsync(descendant.Id, ct);

            // A deleted page no longer uses its media: they may now count as unused.
            await pageMedia.RemoveByPageAsync(descendant.Id, ct);

            // A deleted page stops carrying its tags, and a tag nobody carries anymore goes with
            // it unless it kept a color.
            await PageTagSynchronizer.ClearAsync(descendant.Id, userId, tags, pageTags, ct);
        }
    }
}
