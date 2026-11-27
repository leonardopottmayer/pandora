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

namespace Pottmayer.Pandora.Modules.Finances.Application.Commands.UndoPositionMovement;

public sealed class UndoPositionMovementCommandHandler(IUnitOfWorkFactory factory, TimeProvider timeProvider)
    : CommandHandlerBase<UndoPositionMovementCommand, bool>
{
    protected override async Task<Result<bool>> HandleAsync(UndoPositionMovementCommand request, CancellationToken ct)
    {
        var input = request.Input;
        var now = timeProvider.GetUtcNow();

        var result = await factory.ExecuteAsync(FinancesModule.DatabaseKey, async (ctx, token) =>
        {
            var positions = ctx.AcquireRepository<IPositionRepository>();
            var position = await positions.FindByIdForUserAsync(input.PositionId, input.UserId, token);
            if (position is null) return Result<bool>.Failure([PositionErrors.NotFound]);

            var account = await ctx.AcquireRepository<IAccountRepository>().FindByIdForUserAsync(position.AccountId, input.UserId, token);
            if (account is null) return Result<bool>.Failure([AccountErrors.NotFound]);
            if (account.IsArchived) return Result<bool>.Failure([AccountErrors.Archived]);

            var movements = ctx.AcquireRepository<IPositionMovementRepository>();
            var all = await movements.ListByPositionAsync(position.Id, token);
            if (all.Count == 0) return Result<bool>.Failure([PositionErrors.NotFound]);
            var latest = all[^1];

            var correlationId = Guid.CreateVersion7();
            await PositionLedger.VoidAsync(ctx, latest, timeProvider, correlationId, token);
            await movements.RemoveAsync(latest, token);
            var details = new { movementId = latest.Id, kind = latest.Kind.Value, latest.OccurredOn, latest.NetAmount, transactionId = latest.TransactionId };

            if (all.Count == 1)
            {
                // The only movement: nothing is left of the position, so it goes with its values.
                var values = ctx.AcquireRepository<IPositionValuationRepository>();
                foreach (var value in await values.ListByPositionAsync(position.Id, token))
                    await values.RemoveAsync(value, token);
                await positions.RemoveAsync(position, token);
                await ctx.RecordAsync(input.UserId, input.UserId, PositionEvents.EntityType, position.Id, PositionEvents.Deleted, now, details, correlationId, token);
                return Result<bool>.Success(false);
            }

            var undone = position.Undo(latest);
            if (undone.IsFailure) return Result<bool>.Failure([.. undone.Errors]);
            await positions.UpdateAsync(position, token);
            await ctx.RecordAsync(input.UserId, input.UserId, PositionEvents.EntityType, position.Id, PositionEvents.MovementUndone, now, details, correlationId, token);
            return Result<bool>.Success(true);
        }, cancellationToken: ct);

        return result.IsFailure ? Fail([.. result.Errors]) : Ok(result.Value);
    }
}
