using Pottmayer.Pandora.Modules.Files.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Security.Authorization.Abstractions;
using Pottmayer.Pandora.Modules.Files.Abstractions;

namespace Pottmayer.Pandora.Modules.Files.Application.Queries.GetReviewTree;

/// <param name="RootId">Null lists the roots with something to review.</param>
/// <param name="ParentPath">The folder to expand; null is the root.</param>
public sealed record GetReviewTreeInput(Guid UserId, Guid? RootId, string? ParentPath);

[RequiresPermission(FilesPermissions.Review.Read)]
public sealed class GetReviewTreeQuery(GetReviewTreeInput input)
    : QueryBase<GetReviewTreeInput, IReadOnlyList<ReviewNodeDto>>(input);
