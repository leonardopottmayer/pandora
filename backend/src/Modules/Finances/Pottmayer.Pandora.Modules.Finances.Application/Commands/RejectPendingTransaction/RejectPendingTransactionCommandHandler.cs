using Pottmayer.Pandora.Modules.Finances.Abstractions;
using Pottmayer.Pandora.Modules.Finances.Application.Auditing;
using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Pandora.Modules.Finances.Domain.Errors;
using Pottmayer.Pandora.Modules.Finances.Domain.Ports.Repositories;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Finances.Application.Commands.RejectPendingTransaction;

public sealed class RejectPendingTransactionCommandHandler(
    IUnitOfWorkFactory factory,
    TimeProvider timeProvider)
    : CommandHandlerBase<RejectPendingTransactionCommand, PendingTransactionDto>
{
    protected override async Task<Result<PendingTransactionDto>> HandleAsync(
        RejectPendingTransactionCommand request, CancellationToken ct)
    {
        var input = request.Input;
        var now = timeProvider.GetUtcNow();

        var result = await factory.ExecuteAsync(FinancesModule.Name, async (ctx, token) =>
        {
            var repo = ctx.AcquireRepository<IPendingTransactionRepository>();
            var pending = await repo.FindByIdForUserAsync(input.Id, input.UserId, token);
            if (pending is null) return Result<Domain.Aggregates.PendingTransaction>.Failure([PendingTransactionErrors.NotFound]);
            if (!pending.IsPending) return Result<Domain.Aggregates.PendingTransaction>.Failure([PendingTransactionErrors.AlreadyDecided]);

            pending.Reject(input.Reason, input.UserId, timeProvider);
            await repo.UpdateAsync(pending, token);

            await ctx.RecordAsync(input.UserId, input.UserId, "pending-transaction", pending.Id,
                "pending.rejected", now, new { input.Reason }, ct: token);

            return Result<Domain.Aggregates.PendingTransaction>.Success(pending);
        }, cancellationToken: ct);

        return result.IsFailure ? Fail([.. result.Errors]) : Ok(PendingTransactionDto.From(result.Value!));
    }
}
