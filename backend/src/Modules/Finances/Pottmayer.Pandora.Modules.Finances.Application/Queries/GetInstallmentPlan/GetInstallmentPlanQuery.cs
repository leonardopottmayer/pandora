using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Security.Authorization.Abstractions;
using Pottmayer.Pandora.Modules.Finances.Abstractions;

namespace Pottmayer.Pandora.Modules.Finances.Application.Queries.GetInstallmentPlan;

public sealed record GetInstallmentPlanInput(Guid UserId, Guid PlanId);

[RequiresPermission(FinancesPermissions.Cards.Read)]
public sealed class GetInstallmentPlanQuery(GetInstallmentPlanInput input)
    : QueryBase<GetInstallmentPlanInput, InstallmentPlanDto>(input);
