using Pottmayer.Pandora.Modules.Finances.Abstractions;
using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Security.Authorization.Abstractions;

namespace Pottmayer.Pandora.Modules.Finances.Application.Queries.GetPositions;

/// <param name="IncludeClosed">Closed positions too (default: only open ones).</param>
public sealed record GetPositionsInput(Guid UserId, Guid? AccountId, Guid? AssetClassId, bool IncludeClosed);

[RequiresPermission(FinancesPermissions.Investments.Read)]
public sealed class GetPositionsQuery(GetPositionsInput input) : QueryBase<GetPositionsInput, IReadOnlyList<PositionDto>>(input);
