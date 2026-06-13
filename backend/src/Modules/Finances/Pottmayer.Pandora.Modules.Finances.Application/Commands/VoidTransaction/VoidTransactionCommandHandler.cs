using Pottmayer.Pandora.Modules.Finances.Abstractions;
using Pottmayer.Pandora.Modules.Finances.Application.Auditing;
using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Finances.Domain.Errors;
using Pottmayer.Pandora.Modules.Finances.Domain.Ports.Repositories;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Finances.Application.Commands.VoidTransaction;

public sealed class VoidTransactionCommandHandler(IUnitOfWorkFactory factory, TimeProvider timeProvider)
    : CommandHandlerBase<VoidTransactionCommand, TransactionDto>
{
    protected override async Task<Result<TransactionDto>> HandleAsync(
        VoidTransactionCommand request, CancellationToken ct)
    {
        var input = request.Input;
        var now = timeProvider.GetUtcNow();

        var result = await factory.ExecuteAsync(FinancesModule.Name, async (ctx, token) =>
        {
            var repo = ctx.AcquireRepository<ITransactionRepository>();

            var transaction = await repo.FindByIdForUserAsync(input.TransactionId, input.UserId, token);
            if (transaction is null)
                return Result<Transaction>.Failure([TransactionErrors.NotFound]);

            if (transaction.IsVoid)
                return Result<Transaction>.Failure([TransactionErrors.AlreadyVoid]);

            // A transfer is voided as a unit: cancelling one leg cancels its partner in the same UoW.
            var toVoid = transaction.TransferGroupId is null
                ? [transaction]
                : await repo.GetByTransferGroupAsync(transaction.TransferGroupId.Value, input.UserId, token);

            var correlationId = Guid.CreateVersion7();

            foreach (var entry in toVoid)
            {
                if (!entry.Void(input.Reason, timeProvider)) continue;
                await repo.UpdateAsync(entry, token);
                await ctx.RecordAsync(
                    input.UserId, input.UserId, "transaction", entry.Id, "transaction.voided", now,
                    new { reason = input.Reason }, correlationId, token);
            }

            return Result<Transaction>.Success(transaction);
        }, cancellationToken: ct);

        return result.IsFailure
            ? Fail([.. result.Errors])
            : Ok(TransactionDto.From(result.Value!));
    }
}
