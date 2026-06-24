using System.Text.Json;
using Pottmayer.Pandora.Modules.Finances.Abstractions;
using Pottmayer.Pandora.Modules.Finances.Application.Auditing;
using Pottmayer.Pandora.Modules.Finances.Application.Services;
using Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Finances.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Finances.Domain.Ports.Services;
using Pottmayer.Pandora.Modules.Finances.Domain.ValueObjects;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Finances.Application.Commands.RunRecurrenceGeneration;

public sealed class RunRecurrenceGenerationCommandHandler(
    IUnitOfWorkFactory factory,
    IStatementResolver resolver,
    TimeProvider timeProvider)
    : CommandHandlerBase<RunRecurrenceGenerationCommand, int>
{
    protected override async Task<Result<int>> HandleAsync(
        RunRecurrenceGenerationCommand request, CancellationToken ct)
    {
        var today = request.Input.Today;
        var horizon = today.AddDays(request.Input.HorizonDays);
        var now = timeProvider.GetUtcNow();
        var generated = 0;

        var result = await factory.ExecuteAsync(FinancesModule.Name, async (ctx, token) =>
        {
            var recurringRepo = ctx.AcquireRepository<IRecurringTransactionRepository>();
            var pendingRepo = ctx.AcquireRepository<IPendingTransactionRepository>();
            var transactionRepo = ctx.AcquireRepository<ITransactionRepository>();
            var accountRepo = ctx.AcquireRepository<IAccountRepository>();
            var cardRepo = ctx.AcquireRepository<ICardRepository>();
            var statementRepo = ctx.AcquireRepository<ICardStatementRepository>();

            // 1. Post pending account-targeted transactions whose date has been reached (phase-04 pendency)
            var duePending = await transactionRepo.GetDuePendingAsync(today, token);
            foreach (var tx in duePending)
            {
                tx.Post(timeProvider);
                await transactionRepo.UpdateAsync(tx, token);
                await ctx.RecordAsync(tx.UserId, null, TransactionEvents.EntityType, tx.Id,
                    TransactionEvents.Posted, now, new { source = "auto-post-scheduled" }, ct: token);
                generated++;
            }

            // 2. Generate pending / auto-post for active recurring transactions within the horizon
            var candidates = await recurringRepo.GetActiveWithOccurrencesBeforeAsync(horizon, token);

            foreach (var recurring in candidates)
            {
                var rule = recurring.GetRule();
                var cursor = recurring.NextOccurrenceOn;

                // Walks every due date up to the horizon, advancing the cursor each iteration —
                // this is what catches up a template that missed several runs (e.g. job was down).
                while (!rule.IsTerminated(cursor, recurring.OccurrencesCount) && cursor <= horizon)
                {
                    // A manual generation (GenerateRecurringTransactionOccurrence) may have already
                    // produced this date's suggestion; skip it instead of duplicating.
                    var alreadyExists = await pendingRepo.ExistsForRecurrenceOnDateAsync(recurring.Id, cursor, token);
                    if (!alreadyExists)
                    {
                        var targetIsCard = recurring.CardId is not null;
                        var autoPostNow = recurring.AutoPost && !targetIsCard;

                        if (autoPostNow)
                        {
                            // auto_post to account: post directly as a transaction
                            var account = await accountRepo.FindByIdForUserAsync(recurring.AccountId!.Value, recurring.UserId, token);
                            if (account is not null)
                            {
                                var tx = Transaction.CreateAccountTransaction(
                                    recurring.UserId,
                                    recurring.AccountId!.Value,
                                    TransactionKind.FromValue(recurring.Kind),
                                    account.Currency,
                                    recurring.Amount!.Value,
                                    cursor,
                                    recurring.Description,
                                    recurring.Payee,
                                    notes: null,
                                    recurring.SystemCategoryId,
                                    recurring.UserCategoryId,
                                    post: true,
                                    timeProvider);

                                tx.MarkAsRecurrence(recurring.Id, pendingTransactionId: null);
                                await transactionRepo.AddAsync(tx, token);
                                await ctx.RecordAsync(recurring.UserId, null, TransactionEvents.EntityType, tx.Id,
                                    TransactionEvents.Created, now, new { origin = "recurrence", recurring.Id }, ct: token);
                                await ctx.RecordAsync(recurring.UserId, null, RecurringTransactionEvents.EntityType, recurring.Id,
                                    RecurringTransactionEvents.OccurrenceGenerated, now, new { date = cursor, autoPosted = true, transactionId = tx.Id }, ct: token);
                                generated++;
                            }
                        }
                        else
                        {
                            // Always inbox: account with auto_post=false, or any card target
                            string currency;
                            Guid? suggestedStatementId = null;

                            if (targetIsCard)
                            {
                                var card = await cardRepo.FindByIdForUserAsync(recurring.CardId!.Value, recurring.UserId, token);
                                if (card is null) break;
                                currency = card.Currency.Value;

                                var resolution = await StatementMaintenance.EnsureStatementForPurchaseAsync(
                                    statementRepo, resolver, card, recurring.UserId, cursor,
                                    forcedStatementId: null, timeProvider, token);
                                if (resolution.IsSuccess)
                                    suggestedStatementId = resolution.Value!.Statement.Id;
                            }
                            else
                            {
                                var account = await accountRepo.FindByIdForUserAsync(recurring.AccountId!.Value, recurring.UserId, token);
                                if (account is null) break;
                                currency = account.Currency.Value;
                            }

                            var payload = SerializePayload(recurring, cursor, currency);

                            var pending = PendingTransaction.CreateFromRecurrence(
                                recurring.UserId,
                                recurring.Id,
                                recurring.AccountId,
                                recurring.CardId,
                                recurring.Kind,
                                recurring.Amount,
                                currency,
                                cursor,
                                recurring.Description,
                                recurring.Payee,
                                recurring.SystemCategoryId,
                                recurring.UserCategoryId,
                                suggestedStatementId,
                                payload,
                                timeProvider);

                            await pendingRepo.AddAsync(pending, token);
                            await ctx.RecordAsync(recurring.UserId, null, PendingTransactionEvents.EntityType, pending.Id,
                                PendingTransactionEvents.Created, now, new { source = "recurrence", recurringTransactionId = recurring.Id, date = cursor }, ct: token);
                            await ctx.RecordAsync(recurring.UserId, null, RecurringTransactionEvents.EntityType, recurring.Id,
                                RecurringTransactionEvents.OccurrenceGenerated, now, new { date = cursor, autoPosted = false, pendingId = pending.Id }, ct: token);
                            generated++;
                        }
                    }

                    recurring.AdvanceCursor(cursor);
                    cursor = recurring.NextOccurrenceOn;
                }

                await recurringRepo.UpdateAsync(recurring, token);

                if (recurring.IsFinished)
                    await ctx.RecordAsync(recurring.UserId, null, RecurringTransactionEvents.EntityType, recurring.Id,
                        RecurringTransactionEvents.Finished, now, ct: token);
            }

            return Result<int>.Success(generated);
        }, cancellationToken: ct);

        return result.IsFailure ? Fail([.. result.Errors]) : Ok(result.Value);
    }

    private static string SerializePayload(Domain.Aggregates.RecurringTransaction r, DateOnly occurredOn, string currency) =>
        JsonSerializer.Serialize(new
        {
            r.AccountId,
            r.CardId,
            r.Kind,
            r.Amount,
            Currency = currency,
            OccurredOn = occurredOn,
            r.Description,
            r.Payee,
            r.SystemCategoryId,
            r.UserCategoryId
        });
}
