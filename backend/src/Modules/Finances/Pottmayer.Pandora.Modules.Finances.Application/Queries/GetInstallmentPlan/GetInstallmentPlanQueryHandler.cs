using Pottmayer.Pandora.Modules.Finances.Abstractions;
using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Pandora.Modules.Finances.Application.Services;
using Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Finances.Domain.Errors;
using Pottmayer.Pandora.Modules.Finances.Domain.Ports.Repositories;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Finances.Application.Queries.GetInstallmentPlan;

public sealed class GetInstallmentPlanQueryHandler(IUnitOfWorkFactory factory)
    : QueryHandlerBase<GetInstallmentPlanQuery, InstallmentPlanDto>
{
    protected override async Task<Result<InstallmentPlanDto>> HandleAsync(GetInstallmentPlanQuery request, CancellationToken ct)
    {
        var input = request.Input;
        var result = await factory.ExecuteAsync(FinancesModule.Name, async (ctx, token) =>
        {
            var plans = ctx.AcquireRepository<IInstallmentPlanRepository>();
            var plan = await plans.FindByIdForUserAsync(input.PlanId, input.UserId, token);
            if (plan is null)
                return Result<InstallmentPlanDto>.Failure([InstallmentErrors.NotFound]);

            var transactions = ctx.AcquireRepository<ITransactionRepository>();
            var statements = ctx.AcquireRepository<ICardStatementRepository>();

            var installments = await transactions.GetByInstallmentPlanAsync(plan.Id, input.UserId, token);
            var byStatement = new Dictionary<Guid, CardStatement>();
            foreach (var statementId in installments.Where(t => t.CardStatementId is not null).Select(t => t.CardStatementId!.Value).Distinct())
            {
                var statement = await statements.FindByIdForUserAsync(statementId, input.UserId, token);
                if (statement is not null) byStatement[statementId] = statement;
            }

            return Result<InstallmentPlanDto>.Success(InstallmentPlanAssembler.Assemble(plan, installments, byStatement));
        }, cancellationToken: ct);

        return result.IsFailure ? Fail([.. result.Errors]) : Ok(result.Value!);
    }
}
