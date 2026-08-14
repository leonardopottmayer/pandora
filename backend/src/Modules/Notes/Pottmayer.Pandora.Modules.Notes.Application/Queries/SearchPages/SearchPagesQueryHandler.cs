using Pottmayer.Pandora.Modules.Notes.Abstractions;
using Pottmayer.Pandora.Modules.Notes.Application.Dtos;
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

    protected override async Task<Result<IReadOnlyList<PageSearchResultDto>>> HandleAsync(
        SearchPagesQuery request, CancellationToken ct)
    {
        var input = request.Input;
        var tsQuery = PageSearch.ToTsQuery(input.Term);

        if (tsQuery.Length == 0)
            return Ok((IReadOnlyList<PageSearchResultDto>)[]);

        var pages = await factory.ExecuteAsync(NotesModule.Name, async (ctx, token) =>
            await ctx.AcquireRepository<IPageRepository>()
                .SearchAsync(input.UserId, tsQuery, ResultLimit, token),
            cancellationToken: ct);

        IReadOnlyList<PageSearchResultDto> results =
        [
            .. pages.Select(p => new PageSearchResultDto(
                p.Id, p.Title, p.Slug, p.Icon, p.IsArchived,
                PageSearch.Excerpt(p.ContentMarkdown, input.Term)))
        ];

        return Ok(results);
    }
}
