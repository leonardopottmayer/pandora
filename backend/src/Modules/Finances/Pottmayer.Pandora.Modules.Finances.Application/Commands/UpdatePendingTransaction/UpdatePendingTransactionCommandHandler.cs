using Pottmayer.Pandora.Modules.Finances.Abstractions;
using Pottmayer.Pandora.Modules.Finances.Application.Auditing;
using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Pandora.Modules.Finances.Domain.Errors;
using Pottmayer.Pandora.Modules.Finances.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Finances.Domain.ValueObjects;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Finances.Application.Commands.UpdatePendingTransaction;

public sealed class UpdatePendingTransactionCommandHandler(
    IUnitOfWorkFactory factory,
    TimeProvider timeProvider)
    : CommandHandlerBase<UpdatePendingTransactionCommand, PendingTransactionDto>
{
    protected override async Task<Result<PendingTransactionDto>> HandleAsync(
        UpdatePendingTransactionCommand request, CancellationToken ct)
    {
        var input = request.Input;

        if (!TransactionKind.IsSupported(input.Kind))
            return Fail(TransactionErrors.InvalidKind(input.Kind));
        if (string.IsNullOrWhiteSpace(input.Description))
            return Fail(TransactionErrors.InvalidDescription);

        var now = timeProvider.GetUtcNow();

        var result = await factory.ExecuteAsync(FinancesModule.Name, async (ctx, token) =>
        {
            var repo = ctx.AcquireRepository<IPendingTransactionRepository>();
            var pending = await repo.FindByIdForUserAsync(input.Id, input.UserId, token);
            if (pending is null) return Result<Domain.Aggregates.PendingTransaction>.Failure([PendingTransactionErrors.NotFound]);
            if (!pending.IsPending) return Result<Domain.Aggregates.PendingTransaction>.Failure([PendingTransactionErrors.AlreadyDecided]);

            // Captured before the mutation so the audit event shows both the prior and new payload.
            var before = new
            {
                pending.Kind,
                pending.Amount,
                pending.OccurredOn,
                pending.Description,
                pending.Payee,
                pending.Notes,
                pending.SystemCategoryId,
                pending.UserCategoryId,
                pending.SuggestedStatementId
            };

            pending.UpdatePayload(
                input.Kind,
                input.Amount,
                input.OccurredOn,
                input.Description,
                input.Payee,
                input.Notes,
                input.SystemCategoryId,
                input.UserCategoryId,
                input.SuggestedStatementId);

            await repo.UpdateAsync(pending, token);
            await ctx.RecordAsync(input.UserId, input.UserId, PendingTransactionEvents.EntityType, pending.Id,
                PendingTransactionEvents.Edited, now, new { before, after = new
                {
                    pending.Kind,
                    pending.Amount,
                    pending.OccurredOn,
                    pending.Description,
                    pending.Payee,
                    pending.Notes,
                    pending.SystemCategoryId,
                    pending.UserCategoryId,
                    pending.SuggestedStatementId
                } }, ct: token);

            return Result<Domain.Aggregates.PendingTransaction>.Success(pending);
        }, cancellationToken: ct);

        return result.IsFailure ? Fail([.. result.Errors]) : Ok(PendingTransactionDto.From(result.Value!));
    }
}
