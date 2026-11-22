using Pottmayer.Pandora.Modules.Admin.Application.Dtos;
using Pottmayer.Pandora.Modules.Admin.Abstractions;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Security.Authorization.Abstractions;

namespace Pottmayer.Pandora.Modules.Admin.Application.Queries.GetFeatures;

public sealed record GetFeaturesInput(Guid UserId);

/// <summary>Every declared flag with its default, its override and its effective state.</summary>
[RequiresPermission(AdminPermissions.Features.Manage)]
public sealed class GetFeaturesQuery(GetFeaturesInput input)
    : QueryBase<GetFeaturesInput, IReadOnlyList<FeatureDto>>(input);
