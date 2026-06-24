using Pottmayer.Pandora.Modules.Finances.Abstractions;
using Pottmayer.Pandora.Modules.Finances.Application.Auditing;
using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Finances.Domain.Errors;
using Pottmayer.Pandora.Modules.Finances.Domain.Ports.Repositories;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Finances.Application.Commands.UpdateTransaction;

public sealed class UpdateTransactionCommandHandler(IUnitOfWorkFactory factory, TimeProvider timeProvider)
    : CommandHandlerBase<UpdateTransactionCommand, TransactionDto>
{
    protected override async Task<Result<TransactionDto>> HandleAsync(
        UpdateTransactionCommand request, CancellationToken ct)
    {
        var input = request.Input;
        var now = timeProvider.GetUtcNow();

        if (string.IsNullOrWhiteSpace(input.Description))
            return Fail(TransactionErrors.InvalidDescription);

        var result = await factory.ExecuteAsync(FinancesModule.Name, async (ctx, token) =>
        {
            var repo = ctx.AcquireRepository<ITransactionRepository>();

            var transaction = await repo.FindByIdForUserAsync(input.TransactionId, input.UserId, token);
            if (transaction is null)
                return Result<Transaction>.Failure([TransactionErrors.NotFound]);

            if (transaction.IsVoid)
                return Result<Transaction>.Failure([TransactionErrors.AlreadyVoid]);

            // Captured before the mutation so the audit event records both sides of the change.
            var diff = new
            {
                description = new { old = transaction.Description, @new = input.Description.Trim() },
                payee = new { old = transaction.Payee, @new = input.Payee },
                notes = new { old = transaction.Notes, @new = input.Notes },
                systemCategoryId = new { old = transaction.SystemCategoryId, @new = input.SystemCategoryId },
                userCategoryId = new { old = transaction.UserCategoryId, @new = input.UserCategoryId }
            };

            transaction.UpdateDetails(
                input.Description, input.Payee, input.Notes, input.SystemCategoryId, input.UserCategoryId);
            await repo.UpdateAsync(transaction, token);

            await ctx.RecordAsync(
                input.UserId, input.UserId, TransactionEvents.EntityType, transaction.Id, TransactionEvents.Edited, now, diff, ct: token);

            return Result<Transaction>.Success(transaction);
        }, cancellationToken: ct);

        return result.IsFailure
            ? Fail([.. result.Errors])
            : Ok(TransactionDto.From(result.Value!));
    }
}
