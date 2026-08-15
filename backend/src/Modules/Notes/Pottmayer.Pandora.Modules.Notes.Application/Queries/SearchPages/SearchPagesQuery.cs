using Pottmayer.Pandora.Modules.Notes.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Queries;

namespace Pottmayer.Pandora.Modules.Notes.Application.Queries.SearchPages;

/// <summary>
/// <paramref name="TagIds"/> narrows the hits to the pages carrying all of them. With tags and no
/// <paramref name="Term"/> the query lists that tag's pages — browsing a tag is a search too.
/// </summary>
public sealed record SearchPagesInput(
    Guid UserId, string? Term, IReadOnlyCollection<Guid>? TagIds = null);

public sealed class SearchPagesQuery(SearchPagesInput input)
    : QueryBase<SearchPagesInput, IReadOnlyList<PageSearchResultDto>>(input);
