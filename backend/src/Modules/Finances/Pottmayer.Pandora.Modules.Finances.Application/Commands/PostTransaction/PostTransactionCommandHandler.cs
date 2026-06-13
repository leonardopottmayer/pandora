using Pottmayer.Pandora.Modules.Finances.Abstractions;
using Pottmayer.Pandora.Modules.Finances.Application.Auditing;
using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Finances.Domain.Errors;
using Pottmayer.Pandora.Modules.Finances.Domain.Ports.Repositories;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Finances.Application.Commands.PostTransaction;

public sealed class PostTransactionCommandHandler(IUnitOfWorkFactory factory, TimeProvider timeProvider)
    : CommandHandlerBase<PostTransactionCommand, TransactionDto>
{
    protected override async Task<Result<TransactionDto>> HandleAsync(
        PostTransactionCommand request, CancellationToken ct)
    {
        var input = request.Input;
        var now = timeProvider.GetUtcNow();

        var result = await factory.ExecuteAsync(FinancesModule.Name, async (ctx, token) =>
        {
            var repo = ctx.AcquireRepository<ITransactionRepository>();

            var transaction = await repo.FindByIdForUserAsync(input.TransactionId, input.UserId, token);
            if (transaction is null)
                return Result<Transaction>.Failure([TransactionErrors.NotFound]);

            if (!transaction.Post(timeProvider))
                return Result<Transaction>.Failure([TransactionErrors.NotPending]);

            await repo.UpdateAsync(transaction, token);

            await ctx.RecordAsync(
                input.UserId, input.UserId, "transaction", transaction.Id, "transaction.posted", now, ct: token);

            return Result<Transaction>.Success(transaction);
        }, cancellationToken: ct);

        return result.IsFailure
            ? Fail([.. result.Errors])
            : Ok(TransactionDto.From(result.Value!));
    }
}
