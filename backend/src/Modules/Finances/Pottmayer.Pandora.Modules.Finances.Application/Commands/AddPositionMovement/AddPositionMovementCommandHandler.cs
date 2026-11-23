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

namespace Pottmayer.Pandora.Modules.Finances.Application.Commands.AddPositionMovement;

public sealed class AddPositionMovementCommandHandler(
    IUnitOfWorkFactory factory, IEffectiveTimeZoneResolver timeZones, TimeProvider timeProvider)
    : CommandHandlerBase<AddPositionMovementCommand, PositionDto>
{
    protected override async Task<Result<PositionDto>> HandleAsync(AddPositionMovementCommand request, CancellationToken ct)
    {
        var input = request.Input;
        if (!PositionMovementKind.IsSupported(input.Kind))
            return Fail(PositionErrors.KindNotAllowed(input.Kind, "any"));
        if (input.IncomeKind is not null && !IncomeKind.IsSupported(input.IncomeKind))
            return Fail(PositionErrors.IncomeKindRequired);

        var now = timeProvider.GetUtcNow();
        var today = await timeZones.ResolveTodayAsync(input.UserId, now, ct);

        var result = await factory.ExecuteAsync(FinancesModule.DatabaseKey, async (ctx, token) =>
        {
            var positions = ctx.AcquireRepository<IPositionRepository>();
            var position = await positions.FindByIdForUserAsync(input.PositionId, input.UserId, token);
            if (position is null) return Result<PositionDto>.Failure([PositionErrors.NotFound]);

            var account = await ctx.AcquireRepository<IAccountRepository>().FindByIdForUserAsync(position.AccountId, input.UserId, token);
            if (account is null) return Result<PositionDto>.Failure([AccountErrors.NotFound]);
            if (account.IsArchived) return Result<PositionDto>.Failure([AccountErrors.Archived]);
            var asset = (await ctx.AcquireRepository<IAssetRepository>().FindByIdForUserAsync(position.AssetId, input.UserId, token))!;

            var kind = PositionMovementKind.FromValue(input.Kind);
            decimal? currentValue = kind == PositionMovementKind.Redemption
                ? await PositionReader.CurrentValueAsync(ctx, position, token)
                : null;

            var applied = position.Apply(new MovementInput(
                kind, input.OccurredOn, input.Quantity, input.UnitPrice, input.GrossAmount, input.Costs, input.TaxWithheld,
                input.IncomeKind is null ? null : IncomeKind.FromValue(input.IncomeKind), input.SplitFactor,
                CurrentValue: currentValue, Notes: input.Notes),
                isFirst: false, timeProvider);
            if (applied.IsFailure) return Result<PositionDto>.Failure([.. applied.Errors]);
            var movement = applied.Value!;

            var correlationId = Guid.CreateVersion7();
            await positions.UpdateAsync(position, token);
            await PositionLedger.LinkAsync(ctx, account, asset, movement, today, timeProvider, correlationId, token);
            await ctx.AcquireRepository<IPositionMovementRepository>().AddAsync(movement, token);

            await ctx.RecordAsync(input.UserId, input.UserId, PositionEvents.EntityType, position.Id, PositionEvents.MovementAdded, now,
                new
                {
                    movementId = movement.Id,
                    kind = movement.Kind.Value,
                    movement.OccurredOn,
                    movement.Quantity,
                    movement.UnitPrice,
                    movement.GrossAmount,
                    movement.Costs,
                    movement.TaxWithheld,
                    movement.NetAmount,
                    movement.RealizedResult,
                    transactionId = movement.TransactionId,
                    status = position.Status.Value
                },
                correlationId, token);

            return Result<PositionDto>.Success(await PositionReader.ReadAsync(ctx, input.UserId, position, token));
        }, cancellationToken: ct);

        return result.IsFailure ? Fail([.. result.Errors]) : Ok(result.Value!);
    }
}
