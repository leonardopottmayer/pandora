using Pottmayer.Pandora.Modules.Notes.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Security.Authorization.Abstractions;
using Pottmayer.Pandora.Modules.Notes.Abstractions;

namespace Pottmayer.Pandora.Modules.Notes.Application.Queries.SearchPages;

/// <summary>
/// <paramref name="TagIds"/> narrows the hits to the pages carrying all of them. With tags and no
/// <paramref name="Term"/> the query lists that tag's pages — browsing a tag is a search too.
/// <paramref name="SpaceId"/> keeps the hits to one space.
/// </summary>
public sealed record SearchPagesInput(
    Guid UserId, string? Term, IReadOnlyCollection<Guid>? TagIds = null, Guid? SpaceId = null);

[RequiresPermission(NotesPermissions.Pages.Read)]
public sealed class SearchPagesQuery(SearchPagesInput input)
    : QueryBase<SearchPagesInput, IReadOnlyList<PageSearchResultDto>>(input);
