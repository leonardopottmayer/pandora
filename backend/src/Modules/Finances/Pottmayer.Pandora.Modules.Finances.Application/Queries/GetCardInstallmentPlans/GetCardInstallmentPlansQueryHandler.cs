using Pottmayer.Pandora.Modules.Finances.Abstractions;
using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Pandora.Modules.Finances.Application.Services;
using Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Finances.Domain.Errors;
using Pottmayer.Pandora.Modules.Finances.Domain.Ports.Repositories;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Finances.Application.Queries.GetCardInstallmentPlans;

public sealed class GetCardInstallmentPlansQueryHandler(IUnitOfWorkFactory factory)
    : QueryHandlerBase<GetCardInstallmentPlansQuery, IReadOnlyList<InstallmentPlanDto>>
{
    protected override async Task<Result<IReadOnlyList<InstallmentPlanDto>>> HandleAsync(
        GetCardInstallmentPlansQuery request, CancellationToken ct)
    {
        var input = request.Input;
        var result = await factory.ExecuteAsync(FinancesModule.Name, async (ctx, token) =>
        {
            var cards = ctx.AcquireRepository<ICardRepository>();
            var card = await cards.FindByIdForUserAsync(input.CardId, input.UserId, token);
            if (card is null)
                return Result<IReadOnlyList<InstallmentPlanDto>>.Failure([CardErrors.NotFound]);

            var plans = await ctx.AcquireRepository<IInstallmentPlanRepository>().GetByCardAsync(input.CardId, input.UserId, token);
            var transactions = ctx.AcquireRepository<ITransactionRepository>();

            var byStatement = (await ctx.AcquireRepository<ICardStatementRepository>().GetByCardAsync(input.CardId, input.UserId, token))
                .ToDictionary(s => s.Id);

            var dtos = new List<InstallmentPlanDto>(plans.Count);
            foreach (var plan in plans)
            {
                var installments = await transactions.GetByInstallmentPlanAsync(plan.Id, input.UserId, token);
                dtos.Add(InstallmentPlanAssembler.Assemble(plan, installments, byStatement));
            }

            return Result<IReadOnlyList<InstallmentPlanDto>>.Success(dtos);
        }, cancellationToken: ct);

        return result.IsFailure ? Fail([.. result.Errors]) : Ok(result.Value!);
    }
}
