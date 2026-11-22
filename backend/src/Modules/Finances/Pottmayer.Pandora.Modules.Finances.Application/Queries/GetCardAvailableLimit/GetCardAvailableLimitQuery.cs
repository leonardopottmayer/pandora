using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Security.Authorization.Abstractions;
using Pottmayer.Pandora.Modules.Finances.Abstractions;

namespace Pottmayer.Pandora.Modules.Finances.Application.Queries.GetCardAvailableLimit;

public sealed record GetCardAvailableLimitInput(Guid UserId, Guid CardId);

[RequiresPermission(FinancesPermissions.Cards.Read)]
public sealed class GetCardAvailableLimitQuery(GetCardAvailableLimitInput input)
    : QueryBase<GetCardAvailableLimitInput, CardAvailableLimitDto>(input);
