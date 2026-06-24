using Pottmayer.Pandora.Modules.Finances.Abstractions;
using Pottmayer.Pandora.Modules.Finances.Application.Auditing;
using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Pandora.Modules.Finances.Domain.Errors;
using Pottmayer.Pandora.Modules.Finances.Domain.Ports.Repositories;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Finances.Application.Commands.PauseRecurringTransaction;

public sealed class PauseRecurringTransactionCommandHandler(
    IUnitOfWorkFactory factory,
    TimeProvider timeProvider)
    : CommandHandlerBase<PauseRecurringTransactionCommand, RecurringTransactionDto>
{
    protected override async Task<Result<RecurringTransactionDto>> HandleAsync(
        PauseRecurringTransactionCommand request, CancellationToken ct)
    {
        var input = request.Input;
        var now = timeProvider.GetUtcNow();

        var result = await factory.ExecuteAsync(FinancesModule.Name, async (ctx, token) =>
        {
            var repo = ctx.AcquireRepository<IRecurringTransactionRepository>();
            var recurring = await repo.FindByIdForUserAsync(input.Id, input.UserId, token);
            if (recurring is null) return Result<Domain.Aggregates.RecurringTransaction>.Failure([RecurringTransactionErrors.NotFound]);
            if (recurring.IsFinished) return Result<Domain.Aggregates.RecurringTransaction>.Failure([RecurringTransactionErrors.Finished]);
            if (!recurring.Pause()) return Result<Domain.Aggregates.RecurringTransaction>.Failure([RecurringTransactionErrors.AlreadyPaused]);

            await repo.UpdateAsync(recurring, token);
            await ctx.RecordAsync(input.UserId, input.UserId, RecurringTransactionEvents.EntityType, recurring.Id,
                RecurringTransactionEvents.Paused, now, ct: token);

            return Result<Domain.Aggregates.RecurringTransaction>.Success(recurring);
        }, cancellationToken: ct);

        return result.IsFailure ? Fail([.. result.Errors]) : Ok(RecurringTransactionDto.From(result.Value!));
    }
}
