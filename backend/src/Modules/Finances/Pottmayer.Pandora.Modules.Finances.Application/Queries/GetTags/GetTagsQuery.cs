using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Security.Authorization.Abstractions;
using Pottmayer.Pandora.Modules.Finances.Abstractions;

namespace Pottmayer.Pandora.Modules.Finances.Application.Queries.GetTags;

public sealed record GetTagsInput(Guid UserId);

[RequiresPermission(FinancesPermissions.Tags.Read)]
public sealed class GetTagsQuery(GetTagsInput input)
    : QueryBase<GetTagsInput, IReadOnlyList<TagDto>>(input);
