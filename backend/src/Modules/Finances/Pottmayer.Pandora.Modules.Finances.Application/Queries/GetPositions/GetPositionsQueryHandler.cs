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

namespace Pottmayer.Pandora.Modules.Finances.Application.Queries.GetPositions;

public sealed class GetPositionsQueryHandler(IUnitOfWorkFactory factory)
    : QueryHandlerBase<GetPositionsQuery, IReadOnlyList<PositionDto>>
{
    protected override async Task<Result<IReadOnlyList<PositionDto>>> HandleAsync(GetPositionsQuery request, CancellationToken ct)
    {
        var input = request.Input;
        var dtos = await factory.ExecuteAsync(FinancesModule.DatabaseKey, async (ctx, token) =>
        {
            var positions = await ctx.AcquireRepository<IPositionRepository>().ListAsync(input.UserId, input.AccountId, !input.IncludeClosed, token);
            var all = await PositionReader.ReadAsync(ctx, input.UserId, positions, token);
            return input.AssetClassId is null ? all : [.. all.Where(p => p.AssetClassId == input.AssetClassId)];
        }, cancellationToken: ct);
        return Ok(dtos);
    }
}
