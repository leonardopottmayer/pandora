using Pottmayer.Pandora.Modules.Finances.Abstractions;
using Pottmayer.Pandora.Modules.Finances.Application.Auditing;
using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Pandora.Modules.Finances.Domain.Errors;
using Pottmayer.Pandora.Modules.Finances.Domain.Ports.Repositories;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Finances.Application.Commands.ResumeRecurringTransaction;

public sealed class ResumeRecurringTransactionCommandHandler(
    IUnitOfWorkFactory factory,
    TimeProvider timeProvider)
    : CommandHandlerBase<ResumeRecurringTransactionCommand, RecurringTransactionDto>
{
    protected override async Task<Result<RecurringTransactionDto>> HandleAsync(
        ResumeRecurringTransactionCommand request, CancellationToken ct)
    {
        var input = request.Input;
        var now = timeProvider.GetUtcNow();

        var result = await factory.ExecuteAsync(FinancesModule.Name, async (ctx, token) =>
        {
            var repo = ctx.AcquireRepository<IRecurringTransactionRepository>();
            var recurring = await repo.FindByIdForUserAsync(input.Id, input.UserId, token);
            if (recurring is null) return Result<Domain.Aggregates.RecurringTransaction>.Failure([RecurringTransactionErrors.NotFound]);
            if (recurring.IsFinished) return Result<Domain.Aggregates.RecurringTransaction>.Failure([RecurringTransactionErrors.Finished]);
            if (!recurring.Resume()) return Result<Domain.Aggregates.RecurringTransaction>.Failure([RecurringTransactionErrors.AlreadyActive]);

            await repo.UpdateAsync(recurring, token);
            await ctx.RecordAsync(input.UserId, input.UserId, RecurringTransactionEvents.EntityType, recurring.Id,
                RecurringTransactionEvents.Resumed, now, ct: token);

            return Result<Domain.Aggregates.RecurringTransaction>.Success(recurring);
        }, cancellationToken: ct);

        return result.IsFailure ? Fail([.. result.Errors]) : Ok(RecurringTransactionDto.From(result.Value!));
    }
}
