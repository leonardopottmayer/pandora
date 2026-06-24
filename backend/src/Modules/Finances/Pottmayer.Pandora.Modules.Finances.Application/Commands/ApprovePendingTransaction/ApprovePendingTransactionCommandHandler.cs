using Pottmayer.Pandora.Modules.Finances.Abstractions;
using Pottmayer.Pandora.Modules.Finances.Application.Auditing;
using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Pandora.Modules.Finances.Application.Services;
using Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Finances.Domain.Errors;
using Pottmayer.Pandora.Modules.Finances.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Finances.Domain.Ports.Services;
using Pottmayer.Pandora.Modules.Finances.Domain.ValueObjects;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Finances.Application.Commands.ApprovePendingTransaction;

public sealed class ApprovePendingTransactionCommandHandler(
    IUnitOfWorkFactory factory,
    IStatementResolver resolver,
    TimeProvider timeProvider)
    : CommandHandlerBase<ApprovePendingTransactionCommand, TransactionDto>
{
    protected override async Task<Result<TransactionDto>> HandleAsync(
        ApprovePendingTransactionCommand request, CancellationToken ct)
    {
        var input = request.Input;
        var now = timeProvider.GetUtcNow();

        var result = await factory.ExecuteAsync(FinancesModule.Name, async (ctx, token) =>
        {
            var pendingRepo = ctx.AcquireRepository<IPendingTransactionRepository>();
            var txRepo = ctx.AcquireRepository<ITransactionRepository>();

            var pending = await pendingRepo.FindByIdForUserAsync(input.Id, input.UserId, token);
            if (pending is null) return Result<Transaction>.Failure([PendingTransactionErrors.NotFound]);
            if (!pending.IsPending) return Result<Transaction>.Failure([PendingTransactionErrors.AlreadyDecided]);
            if (pending.Amount is null) return Result<Transaction>.Failure([PendingTransactionErrors.MissingAmount]);

            var kind = TransactionKind.FromValue(pending.Kind);
            Transaction tx;

            // Every field of the new transaction is copied from the suggestion as it stands now —
            // any edits made via UpdatePendingTransaction are already baked into these values.
            if (pending.AccountId is not null)
            {
                var accountRepo = ctx.AcquireRepository<IAccountRepository>();
                var account = await accountRepo.FindByIdForUserAsync(pending.AccountId.Value, input.UserId, token);
                if (account is null) return Result<Transaction>.Failure([AccountErrors.NotFound]);

                tx = Transaction.CreateAccountTransaction(
                    input.UserId,
                    pending.AccountId.Value,
                    kind,
                    CurrencyCode.Create(pending.Currency),
                    pending.Amount.Value,
                    pending.OccurredOn,
                    pending.Description,
                    pending.Payee,
                    pending.Notes,
                    pending.SystemCategoryId,
                    pending.UserCategoryId,
                    post: true,
                    timeProvider);
            }
            else
            {
                // card target
                var cardRepo = ctx.AcquireRepository<ICardRepository>();
                var statementRepo = ctx.AcquireRepository<ICardStatementRepository>();

                var card = await cardRepo.FindByIdForUserAsync(pending.CardId!.Value, input.UserId, token);
                if (card is null) return Result<Transaction>.Failure([CardErrors.NotFound]);

                // Honors the suggestion's own statement hint when present (e.g. an imported row
                // already matched to a specific cycle); otherwise resolves/creates the current one.
                var statementResult = await StatementMaintenance.EnsureStatementForPurchaseAsync(
                    statementRepo, resolver, card, input.UserId, pending.OccurredOn,
                    pending.SuggestedStatementId, timeProvider, token);
                if (statementResult.IsFailure) return Result<Transaction>.Failure([.. statementResult.Errors]);

                var statement = statementResult.Value!.Statement;

                tx = Transaction.CreateStatementTransaction(
                    input.UserId,
                    pending.CardId!.Value,
                    statement.Id,
                    kind,
                    CurrencyCode.Create(pending.Currency),
                    pending.Amount.Value,
                    pending.OccurredOn,
                    pending.Description,
                    pending.Payee,
                    pending.Notes,
                    pending.SystemCategoryId,
                    pending.UserCategoryId,
                    timeProvider);

                statement.SyncAmounts(
                    statement.TotalAmount + pending.Amount.Value * kind.StatementSign,
                    statement.PaidAmount,
                    DateOnly.FromDateTime(now.UtcDateTime),
                    timeProvider);
                await statementRepo.UpdateAsync(statement, token);
            }

            // Links the new transaction back to whatever produced the suggestion, so its provenance
            // is traceable from either the transaction or the original import/recurrence side.
            if (pending.IsImportSource)
                tx.MarkAsImport(pending.Id);
            else
                tx.MarkAsRecurrence(pending.RecurringTransactionId!.Value, pending.Id);
            await txRepo.AddAsync(tx, token);

            pending.Approve(tx.Id, input.UserId, timeProvider);
            await pendingRepo.UpdateAsync(pending, token);

            var origin = pending.IsImportSource ? "import" : "recurrence";
            await ctx.RecordAsync(input.UserId, input.UserId, TransactionEvents.EntityType, tx.Id,
                TransactionEvents.Created, now, new { origin }, ct: token);
            await ctx.RecordAsync(input.UserId, input.UserId, PendingTransactionEvents.EntityType, pending.Id,
                PendingTransactionEvents.Approved, now, new { transactionId = tx.Id }, ct: token);

            return Result<Transaction>.Success(tx);
        }, cancellationToken: ct);

        return result.IsFailure ? Fail([.. result.Errors]) : Ok(TransactionDto.From(result.Value!));
    }
}
