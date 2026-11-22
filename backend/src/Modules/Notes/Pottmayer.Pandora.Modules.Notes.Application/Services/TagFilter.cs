using Pottmayer.Pandora.Modules.Notes.Domain.Ports.Repositories;

namespace Pottmayer.Pandora.Modules.Notes.Application.Services;

/// <summary>
/// Resolves "which pages carry these tags?" for the sidebar, the search and the graph — one rule for
/// the three of them: several tags **intersect** (a page must carry all of them), which is the
/// narrowing behaviour a filter is expected to have.
/// </summary>
internal static class TagFilter
{
    /// <summary>
    /// The pages carrying every tag in <paramref name="tagIds"/>, or <c>null</c> when no tag was
    /// asked for — which callers read as "no filter", not as "nothing matches". The result is not
    /// owner-scoped: callers intersect it with the pages they already read for the user.
    /// </summary>
    public static async Task<HashSet<Guid>?> MatchingPageIdsAsync(
        IReadOnlyCollection<Guid>? tagIds, IPageTagRepository pageTags, CancellationToken ct)
    {
        if (tagIds is null || tagIds.Count == 0)
            return null;

        var wanted = tagIds.Distinct().ToList();
        var rows = await pageTags.GetByTagsAsync(wanted, ct);

        return
        [
            .. rows.GroupBy(r => r.PageId)
                   .Where(g => g.Select(r => r.TagId).Distinct().Count() == wanted.Count)
                   .Select(g => g.Key)
        ];
    }
}
