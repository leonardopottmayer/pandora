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

namespace Pottmayer.Pandora.Modules.Finances.Application.Queries.GetPosition;

public sealed class GetPositionQueryHandler(IUnitOfWorkFactory factory)
    : QueryHandlerBase<GetPositionQuery, PositionDetailDto>
{
    protected override async Task<Result<PositionDetailDto>> HandleAsync(GetPositionQuery request, CancellationToken ct)
    {
        var input = request.Input;
        var dto = await factory.ExecuteAsync(FinancesModule.DatabaseKey, async (ctx, token) =>
        {
            var position = await ctx.AcquireRepository<IPositionRepository>().FindByIdForUserAsync(input.PositionId, input.UserId, token);
            if (position is null) return null;

            var movements = await ctx.AcquireRepository<IPositionMovementRepository>().ListByPositionAsync(position.Id, token);
            var values = await ctx.AcquireRepository<IPositionValuationRepository>().ListByPositionAsync(position.Id, token);
            return new PositionDetailDto(
                await PositionReader.ReadAsync(ctx, input.UserId, position, token),
                [.. movements.Select(PositionMovementDto.From)],
                [.. values.Select(PositionValuationDto.From)]);
        }, cancellationToken: ct);

        return dto is null ? Fail(PositionErrors.NotFound) : Ok(dto);
    }
}
