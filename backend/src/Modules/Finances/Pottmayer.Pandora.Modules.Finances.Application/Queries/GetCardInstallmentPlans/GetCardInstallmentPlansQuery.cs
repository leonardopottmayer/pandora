using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Queries;

namespace Pottmayer.Pandora.Modules.Finances.Application.Queries.GetCardInstallmentPlans;

public sealed record GetCardInstallmentPlansInput(Guid UserId, Guid CardId);

public sealed class GetCardInstallmentPlansQuery(GetCardInstallmentPlansInput input)
    : QueryBase<GetCardInstallmentPlansInput, IReadOnlyList<InstallmentPlanDto>>(input);
