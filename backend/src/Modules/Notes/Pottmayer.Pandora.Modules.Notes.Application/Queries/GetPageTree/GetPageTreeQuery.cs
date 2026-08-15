using Pottmayer.Pandora.Modules.Notes.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Queries;

namespace Pottmayer.Pandora.Modules.Notes.Application.Queries.GetPageTree;

/// <summary>
/// <paramref name="TagIds"/> empty means no tag filter. With tags, only the pages carrying all of
/// them come back — the frontend shows those as a flat list, since filtering a tree by tag leaves
/// matching children with no matching parent.
/// </summary>
public sealed record GetPageTreeInput(
    Guid UserId, bool IncludeArchived, IReadOnlyCollection<Guid>? TagIds = null);

/// <summary>Returns the user's pages as a flat, ordered list; the frontend nests them by parent.</summary>
public sealed class GetPageTreeQuery(GetPageTreeInput input)
    : QueryBase<GetPageTreeInput, IReadOnlyList<PageSummaryDto>>(input);
