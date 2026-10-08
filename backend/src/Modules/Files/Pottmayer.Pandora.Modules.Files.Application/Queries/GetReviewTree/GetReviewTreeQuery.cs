using Pottmayer.Pandora.Modules.Files.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Queries;

namespace Pottmayer.Pandora.Modules.Files.Application.Queries.GetReviewTree;

/// <param name="RootId">Null lists the roots with something to review.</param>
/// <param name="ParentPath">The folder to expand; null is the root.</param>
public sealed record GetReviewTreeInput(Guid UserId, Guid? RootId, string? ParentPath);

public sealed class GetReviewTreeQuery(GetReviewTreeInput input)
    : QueryBase<GetReviewTreeInput, IReadOnlyList<ReviewNodeDto>>(input);
