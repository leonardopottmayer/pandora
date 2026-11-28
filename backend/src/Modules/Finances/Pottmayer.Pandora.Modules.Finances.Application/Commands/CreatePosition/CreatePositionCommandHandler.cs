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

namespace Pottmayer.Pandora.Modules.Finances.Application.Commands.CreatePosition;

public sealed class CreatePositionCommandHandler(
    IUnitOfWorkFactory factory, IEffectiveTimeZoneResolver timeZones, TimeProvider timeProvider)
    : CommandHandlerBase<CreatePositionCommand, PositionDto>
{
    protected override async Task<Result<PositionDto>> HandleAsync(CreatePositionCommand request, CancellationToken ct)
    {
        var input = request.Input;
        if (input.Kind != PositionMovementKind.Opening.Value && input.Kind != PositionMovementKind.Buy.Value)
            return Fail(PositionErrors.FirstMovement);
        if (input.Index is not null && !FixedIncomeIndex.IsSupported(input.Index))
            return Fail(PositionErrors.InvalidAmount);
        if (input.CurrentValue is < 0m) return Fail(PositionErrors.InvalidAmount);

        var now = timeProvider.GetUtcNow();
        var today = await timeZones.ResolveTodayAsync(input.UserId, now, ct);

        var result = await factory.ExecuteAsync(FinancesModule.DatabaseKey, async (ctx, token) =>
        {
            var account = await ctx.AcquireRepository<IAccountRepository>().FindByIdForUserAsync(input.AccountId, input.UserId, token);
            if (account is null) return Result<PositionDto>.Failure([AccountErrors.NotFound]);
            if (account.IsArchived) return Result<PositionDto>.Failure([AccountErrors.Archived]);

            var asset = await ctx.AcquireRepository<IAssetRepository>().FindByIdForUserAsync(input.AssetId, input.UserId, token);
            if (asset is null) return Result<PositionDto>.Failure([AssetErrors.NotFound]);
            if (asset.IsArchived) return Result<PositionDto>.Failure([AssetErrors.Archived]);
            if (asset.Currency != account.Currency) return Result<PositionDto>.Failure([PositionErrors.CurrencyMismatch]);

            var assetClass = await AssetClassLookup.FindAsync(ctx, asset.AssetClassId, input.UserId, token);
            if (assetClass is null) return Result<PositionDto>.Failure([AssetErrors.ClassNotFound]);

            var positions = ctx.AcquireRepository<IPositionRepository>();
            Position position;
            if (assetClass.Behavior == AssetBehavior.Unit)
            {
                if (await positions.FindOpenUnitAsync(account.Id, asset.Id, token) is not null)
                    return Result<PositionDto>.Failure([PositionErrors.OpenUnitExists]);
                position = Position.CreateUnit(input.UserId, account.Id, asset.Id, timeProvider);
            }
            else
            {
                position = Position.CreateApplication(input.UserId, account.Id, asset.Id, input.OccurredOn,
                    input.Index is null ? null : FixedIncomeIndex.FromValue(input.Index),
                    input.IndexPercent, input.SpreadRate, input.MaturityOn, timeProvider);
            }

            var applied = position.Apply(new MovementInput(
                PositionMovementKind.FromValue(input.Kind), input.OccurredOn, Quantity: input.Quantity, UnitPrice: input.UnitPrice,
                Costs: input.Costs, AverageCost: input.AverageCost, InvestedAmount: input.InvestedAmount, Notes: input.Notes),
                isFirst: true, timeProvider);
            if (applied.IsFailure) return Result<PositionDto>.Failure([.. applied.Errors]);
            var movement = applied.Value!;

            var correlationId = Guid.CreateVersion7();
            await positions.AddAsync(position, token);
            await PositionLedger.LinkAsync(ctx, account, asset, position, movement, today, timeProvider, correlationId, token);
            await ctx.AcquireRepository<IPositionMovementRepository>().AddAsync(movement, token);

            // A fixed-income buy is worth what was invested on day one; anything else only when told.
            var firstValue = input.CurrentValue
                ?? (position.Behavior == AssetBehavior.Application && movement.Kind == PositionMovementKind.Buy ? position.InvestedAmount : null);
            PositionValuation? valuation = null;
            if (firstValue is not null)
            {
                valuation = PositionValuation.Create(input.UserId, position.Id, input.OccurredOn, firstValue.Value, ValuationSource.Manual, timeProvider);
                await ctx.AcquireRepository<IPositionValuationRepository>().AddAsync(valuation, token);
            }

            await ctx.RecordAsync(input.UserId, input.UserId, PositionEvents.EntityType, position.Id, PositionEvents.Created, now,
                new
                {
                    accountId = account.Id,
                    assetId = asset.Id,
                    behavior = position.Behavior.Value,
                    kind = movement.Kind.Value,
                    position.Quantity,
                    position.AverageCost,
                    position.InvestedAmount,
                    movement.NetAmount,
                    transactionId = movement.TransactionId,
                    currentValue = firstValue
                },
                correlationId, token);

            // Built in memory: nothing of the new position is in the database until the unit of work saves.
            return Result<PositionDto>.Success(PositionDto.From(position, asset, valuation, position.CurrentValue(valuation, [movement])));
        }, cancellationToken: ct);

        return result.IsFailure ? Fail([.. result.Errors]) : Ok(result.Value!);
    }
}
