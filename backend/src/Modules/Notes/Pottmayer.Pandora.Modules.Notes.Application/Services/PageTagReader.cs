using Pottmayer.Pandora.Modules.Notes.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Notes.Domain.Ports.Repositories;
using Pottmayer.Tars.Data.Abstractions.DataContext;

namespace Pottmayer.Pandora.Modules.Notes.Application.Services;

/// <summary>
/// Reads the tags a page carries, for the handlers that return a page without having recomputed
/// them (opening, moving, favoriting, archiving — none of which touch the markdown). The save path
/// gets them from <see cref="PageTagSynchronizer"/> instead, which just wrote them.
/// </summary>
internal static class PageTagReader
{
    public static async Task<IReadOnlyList<Tag>> LoadAsync(
        IDataContext ctx, Guid pageId, Guid userId, CancellationToken ct)
    {
        var rows = await ctx.AcquireRepository<IPageTagRepository>().GetByPageAsync(pageId, ct);
        if (rows.Count == 0)
            return [];

        return await ctx.AcquireRepository<ITagRepository>()
            .GetByIdsForUserAsync([.. rows.Select(r => r.TagId)], userId, ct);
    }
}
