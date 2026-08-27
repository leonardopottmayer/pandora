using Pottmayer.Pandora.Modules.Notes.Abstractions;
using Pottmayer.Pandora.Modules.Notes.Application.Dtos;
using Pottmayer.Pandora.Modules.Notes.Application.Services;
using Pottmayer.Pandora.Modules.Notes.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Notes.Domain.ValueObjects;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Notes.Application.Queries.SearchPages;

/// <summary>
/// Full-text search over the user's pages, feeding the command palette. A term with nothing
/// searchable in it (blank, or only punctuation) is an empty result, not an error — the palette
/// asks on every keystroke.
/// </summary>
public sealed class SearchPagesQueryHandler(IUnitOfWorkFactory factory)
    : QueryHandlerBase<SearchPagesQuery, IReadOnlyList<PageSearchResultDto>>
{
    /// <summary>A palette shows a short list; more than this is noise the user would not scroll.</summary>
    private const int ResultLimit = 20;

    /// <summary>How much wider the search reads when a tag cut still has to be applied to it.</summary>
    private const int TagFilterSlack = 10;

    protected override async Task<Result<IReadOnlyList<PageSearchResultDto>>> HandleAsync(
        SearchPagesQuery request, CancellationToken ct)
    {
        var input = request.Input;
        var tsQuery = PageSearch.ToTsQuery(input.Term);
        var hasTagFilter = input.TagIds is { Count: > 0 };

        // Nothing to search and no tag to browse: the palette asks on every keystroke.
        if (tsQuery.Length == 0 && !hasTagFilter)
            return Ok((IReadOnlyList<PageSearchResultDto>)[]);

        var pages = await factory.ExecuteAsync(NotesModule.DatabaseKey, async (ctx, token) =>
        {
            var repo = ctx.AcquireRepository<IPageRepository>();
            var tagged = await TagFilter.MatchingPageIdsAsync(
                input.TagIds, ctx.AcquireRepository<IPageTagRepository>(), token);

            // Tags alone: the hits are that tag's pages, by title, capped like any other search.
            if (tsQuery.Length == 0)
                return (await repo.GetByIdsForUserAsync([.. tagged!], input.UserId, token))
                    .Take(ResultLimit).ToList();

            if (tagged is null)
                return [.. await repo.SearchAsync(input.UserId, tsQuery, ResultLimit, token)];

            // The tag cut happens after the search, so the cap has to be raised before it: capping
            // first would let a page that matches both fall outside the window and vanish.
            var hits = await repo.SearchAsync(input.UserId, tsQuery, ResultLimit * TagFilterSlack, token);
            return hits.Where(p => tagged.Contains(p.Id)).Take(ResultLimit).ToList();
        }, cancellationToken: ct);

        IReadOnlyList<PageSearchResultDto> results =
        [
            .. pages.Select(p => new PageSearchResultDto(
                p.Id, p.Title, p.Slug, p.Icon, p.IsArchived,
                PageSearch.Excerpt(p.ContentMarkdown, input.Term)))
        ];

        return Ok(results);
    }
}
