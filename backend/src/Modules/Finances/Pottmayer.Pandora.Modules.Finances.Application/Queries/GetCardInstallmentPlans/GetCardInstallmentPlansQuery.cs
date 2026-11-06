using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Security.Authorization.Abstractions;
using Pottmayer.Pandora.Modules.Finances.Abstractions;

namespace Pottmayer.Pandora.Modules.Finances.Application.Queries.GetCardInstallmentPlans;

public sealed record GetCardInstallmentPlansInput(Guid UserId, Guid CardId);

[RequiresPermission(FinancesPermissions.Cards.Read)]
public sealed class GetCardInstallmentPlansQuery(GetCardInstallmentPlansInput input)
    : QueryBase<GetCardInstallmentPlansInput, IReadOnlyList<InstallmentPlanDto>>(input);
