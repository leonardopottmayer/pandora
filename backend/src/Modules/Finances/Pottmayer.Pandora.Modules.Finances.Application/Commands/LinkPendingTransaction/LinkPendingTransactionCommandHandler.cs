using Pottmayer.Pandora.Modules.Finances.Abstractions;
using Pottmayer.Pandora.Modules.Finances.Application.Auditing;
using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Finances.Domain.Errors;
using Pottmayer.Pandora.Modules.Finances.Domain.Ports.Repositories;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Finances.Application.Commands.LinkPendingTransaction;

/// <summary>
/// Manually reconciles an import suggestion against a transaction the user already has (e.g. a
/// recurring "YouTube Premium" entry the importer didn't recognise). The suggestion is resolved
/// without creating a new transaction, and the originating import row is marked as matched so a
/// future re-import of the same line auto-resolves to that transaction.
/// </summary>
public sealed class LinkPendingTransactionCommandHandler(
    IUnitOfWorkFactory factory,
    TimeProvider timeProvider)
    : CommandHandlerBase<LinkPendingTransactionCommand, PendingTransactionDto>
{
    protected override async Task<Result<PendingTransactionDto>> HandleAsync(
        LinkPendingTransactionCommand request, CancellationToken ct)
    {
        var input = request.Input;
        var now = timeProvider.GetUtcNow();

        var result = await factory.ExecuteAsync(FinancesModule.Name, async (ctx, token) =>
        {
            var pendingRepo = ctx.AcquireRepository<IPendingTransactionRepository>();
            var txRepo = ctx.AcquireRepository<ITransactionRepository>();
            var rowRepo = ctx.AcquireRepository<IImportRowRepository>();

            var pending = await pendingRepo.FindByIdForUserAsync(input.PendingId, input.UserId, token);
            if (pending is null) return Result<PendingTransaction>.Failure([PendingTransactionErrors.NotFound]);
            if (!pending.IsPending) return Result<PendingTransaction>.Failure([PendingTransactionErrors.AlreadyDecided]);
            if (!pending.IsImportSource || pending.ImportRowId is null)
                return Result<PendingTransaction>.Failure([PendingTransactionErrors.NotImportSource]);

            var tx = await txRepo.FindByIdForUserAsync(input.TransactionId, input.UserId, token);
            if (tx is null) return Result<PendingTransaction>.Failure([TransactionErrors.NotFound]);

            // Recording the match on the source row is what lets a future re-import of the same
            // statement line resolve straight to this transaction instead of raising a new suggestion.
            var row = await rowRepo.GetByIdAsync(pending.ImportRowId.Value, token);
            if (row is not null)
            {
                row.MarkMatched(tx.Id);
                await rowRepo.UpdateAsync(row, token);
            }

            pending.MarkLinkedToExisting(tx.Id, input.UserId, timeProvider);
            await pendingRepo.UpdateAsync(pending, token);

            await ctx.RecordAsync(input.UserId, input.UserId, PendingTransactionEvents.EntityType, pending.Id,
                PendingTransactionEvents.Linked, now, new { transactionId = tx.Id }, ct: token);

            return Result<PendingTransaction>.Success(pending);
        }, cancellationToken: ct);

        return result.IsFailure ? Fail([.. result.Errors]) : Ok(PendingTransactionDto.From(result.Value!));
    }
}
