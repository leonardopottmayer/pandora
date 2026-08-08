using Pottmayer.Pandora.Modules.Notes.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Queries;

namespace Pottmayer.Pandora.Modules.Notes.Application.Queries.GetPageTree;

public sealed record GetPageTreeInput(Guid UserId, bool IncludeArchived);

/// <summary>Returns the user's pages as a flat, ordered list; the frontend nests them by parent.</summary>
public sealed class GetPageTreeQuery(GetPageTreeInput input)
    : QueryBase<GetPageTreeInput, IReadOnlyList<PageSummaryDto>>(input);
