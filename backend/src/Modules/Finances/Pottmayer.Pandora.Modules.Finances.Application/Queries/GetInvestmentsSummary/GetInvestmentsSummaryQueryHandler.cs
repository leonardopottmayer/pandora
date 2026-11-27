using Pottmayer.Pandora.Modules.Finances.Abstractions;
using Pottmayer.Pandora.Modules.Finances.Application.Auditing;
using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Pandora.Modules.Finances.Application.Services;
using Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Finances.Domain.Errors;
using Pottmayer.Pandora.Modules.Finances.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Finances.Domain.ValueObjects;
using Pottmayer.Pandora.Modules.Identity.Abstractions.Ports;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.DataContext;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Finances.Application.Queries.GetInvestmentsSummary;

public sealed class GetInvestmentsSummaryQueryHandler(
    IUnitOfWorkFactory factory, IEffectiveTimeZoneResolver timeZones, TimeProvider timeProvider)
    : QueryHandlerBase<GetInvestmentsSummaryQuery, InvestmentsSummaryDto>
{
    private const int MaturitiesShown = 5;

    protected override async Task<Result<InvestmentsSummaryDto>> HandleAsync(GetInvestmentsSummaryQuery request, CancellationToken ct)
    {
        var userId = request.Input.UserId;
        var today = await timeZones.ResolveTodayAsync(userId, timeProvider.GetUtcNow(), ct);

        var open = await factory.ExecuteAsync(FinancesModule.DatabaseKey, async (ctx, token) =>
        {
            var positions = await ctx.AcquireRepository<IPositionRepository>().ListAsync(userId, null, openOnly: true, token);
            return await PositionReader.ReadAsync(ctx, userId, positions, token);
        }, cancellationToken: ct);

        var totals = open.GroupBy(p => p.Currency)
            .Select(g => new InvestmentTotalDto(g.Key, g.Sum(p => p.CurrentValue), g.Sum(p => p.CostBasis)))
            .OrderByDescending(t => t.Value).ToList();
        var byClass = open.GroupBy(p => (p.AssetClassId, p.Currency))
            .Select(g => new InvestmentGroupDto(g.Key.AssetClassId, g.Key.Currency, g.Sum(p => p.CurrentValue)))
            .OrderByDescending(g => g.Value).ToList();
        var byAccount = open.GroupBy(p => (p.AccountId, p.Currency))
            .Select(g => new InvestmentGroupDto(g.Key.AccountId, g.Key.Currency, g.Sum(p => p.CurrentValue)))
            .OrderByDescending(g => g.Value).ToList();
        var maturities = open.Where(p => p.MaturityOn >= today)
            .OrderBy(p => p.MaturityOn).Take(MaturitiesShown).ToList();

        return Ok(new InvestmentsSummaryDto(totals, byClass, byAccount, maturities));
    }
}
