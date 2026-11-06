using Pottmayer.Pandora.Modules.Notes.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Security.Authorization.Abstractions;
using Pottmayer.Pandora.Modules.Notes.Abstractions;

namespace Pottmayer.Pandora.Modules.Notes.Application.Queries.GetPageTree;

/// <summary>
/// <paramref name="TagIds"/> empty means no tag filter. With tags, only the pages carrying all of
/// them come back — the frontend shows those as a flat list, since filtering a tree by tag leaves
/// matching children with no matching parent. <paramref name="SpaceId"/> keeps one space's pages;
/// without it the list spans every space, which is what wikilink resolution needs.
/// </summary>
public sealed record GetPageTreeInput(
    Guid UserId, bool IncludeArchived, IReadOnlyCollection<Guid>? TagIds = null, Guid? SpaceId = null);

/// <summary>Returns the user's pages as a flat, ordered list; the frontend nests them by parent.</summary>
[RequiresPermission(NotesPermissions.Pages.Read)]
public sealed class GetPageTreeQuery(GetPageTreeInput input)
    : QueryBase<GetPageTreeInput, IReadOnlyList<PageSummaryDto>>(input);
