using Pottmayer.Pandora.Modules.Finances.Abstractions;
using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Security.Authorization.Abstractions;

namespace Pottmayer.Pandora.Modules.Finances.Application.Queries.GetAssets;

public sealed record GetAssetsInput(Guid UserId, Guid? AssetClassId, string? Search);

[RequiresPermission(FinancesPermissions.Investments.Read)]
public sealed class GetAssetsQuery(GetAssetsInput input) : QueryBase<GetAssetsInput, IReadOnlyList<AssetDto>>(input);
