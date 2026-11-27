using Pottmayer.Pandora.Modules.Finances.Abstractions;
using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Security.Authorization.Abstractions;

namespace Pottmayer.Pandora.Modules.Finances.Application.Queries.GetAssetClasses;

public sealed record GetAssetClassesInput(Guid UserId);

/// <summary>System classes plus the user's own, archived ones included.</summary>
[RequiresPermission(FinancesPermissions.Investments.Read)]
public sealed class GetAssetClassesQuery(GetAssetClassesInput input) : QueryBase<GetAssetClassesInput, IReadOnlyList<AssetClassDto>>(input);
