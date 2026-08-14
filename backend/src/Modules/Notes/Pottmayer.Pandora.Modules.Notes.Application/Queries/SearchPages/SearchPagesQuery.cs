using Pottmayer.Pandora.Modules.Notes.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Queries;

namespace Pottmayer.Pandora.Modules.Notes.Application.Queries.SearchPages;

public sealed record SearchPagesInput(Guid UserId, string? Term);

public sealed class SearchPagesQuery(SearchPagesInput input)
    : QueryBase<SearchPagesInput, IReadOnlyList<PageSearchResultDto>>(input);
