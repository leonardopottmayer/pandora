using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Queries;

namespace Pottmayer.Pandora.Modules.Finances.Application.Queries.GetInstallmentPlan;

public sealed record GetInstallmentPlanInput(Guid UserId, Guid PlanId);

public sealed class GetInstallmentPlanQuery(GetInstallmentPlanInput input)
    : QueryBase<GetInstallmentPlanInput, InstallmentPlanDto>(input);
