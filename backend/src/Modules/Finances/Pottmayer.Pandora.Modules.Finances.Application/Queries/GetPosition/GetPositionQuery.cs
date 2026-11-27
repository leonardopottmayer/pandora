using Pottmayer.Pandora.Modules.Finances.Abstractions;
using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Security.Authorization.Abstractions;

namespace Pottmayer.Pandora.Modules.Finances.Application.Queries.GetPosition;

public sealed record GetPositionInput(Guid UserId, Guid PositionId);

[RequiresPermission(FinancesPermissions.Investments.Read)]
public sealed class GetPositionQuery(GetPositionInput input) : QueryBase<GetPositionInput, PositionDetailDto>(input);
