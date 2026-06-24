using System.Text.Json;
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

namespace Pottmayer.Pandora.Modules.Finances.Application.Commands.GenerateRecurringTransactionOccurrence;

public sealed class GenerateRecurringTransactionOccurrenceCommandHandler(
    IUnitOfWorkFactory factory,
    IStatementResolver resolver,
    TimeProvider timeProvider)
    : CommandHandlerBase<GenerateRecurringTransactionOccurrenceCommand, GeneratedOccurrenceDto>
{
    private const string ToInbox = "inbox";
    private const string ToTransactions = "transactions";

    protected override async Task<Result<GeneratedOccurrenceDto>> HandleAsync(
        GenerateRecurringTransactionOccurrenceCommand request, CancellationToken ct)
    {
        var input = request.Input;

        if (input.Destination is not (ToInbox or ToTransactions))
            return Fail(RecurringTransactionErrors.InvalidDestination);

        var now = timeProvider.GetUtcNow();

        var result = await factory.ExecuteAsync(FinancesModule.Name, async (ctx, token) =>
        {
            var recurringRepo = ctx.AcquireRepository<IRecurringTransactionRepository>();
            var recurring = await recurringRepo.FindByIdForUserAsync(input.RecurringTransactionId, input.UserId, token);
            if (recurring is null) return Result<GeneratedOccurrenceDto>.Failure([RecurringTransactionErrors.NotFound]);
            if (recurring.IsFinished) return Result<GeneratedOccurrenceDto>.Failure([RecurringTransactionErrors.Finished]);

            var scheduledDate = recurring.NextOccurrenceOn;
            var occurredOn = input.OccurredOn ?? scheduledDate;
            var amount = input.Amount ?? recurring.Amount;
            var description = string.IsNullOrWhiteSpace(input.Description) ? recurring.Description : input.Description.Trim();
            var payee = input.Payee ?? recurring.Payee;
            var systemCategoryId = input.SystemCategoryId ?? recurring.SystemCategoryId;
            var userCategoryId = input.UserCategoryId ?? recurring.UserCategoryId;

            var kind = TransactionKind.FromValue(recurring.Kind);
            var targetIsCard = recurring.CardId is not null;

            // Resolves the destination's currency and, for a card target, also ensures the
            // statement the occurrence would land on exists.
            string currency;
            Guid? suggestedStatementId = null;
            CardStatement? cardStatement = null;
            var cardStatementCreated = false;

            if (targetIsCard)
            {
                var cardRepo = ctx.AcquireRepository<ICardRepository>();
                var statementRepo = ctx.AcquireRepository<ICardStatementRepository>();
                var card = await cardRepo.FindByIdForUserAsync(recurring.CardId!.Value, input.UserId, token);
                if (card is null) return Result<GeneratedOccurrenceDto>.Failure([CardErrors.NotFound]);

                var resolution = await StatementMaintenance.EnsureStatementForPurchaseAsync(
                    statementRepo, resolver, card, input.UserId, occurredOn,
                    forcedStatementId: null, timeProvider, token);
                if (resolution.IsFailure) return Result<GeneratedOccurrenceDto>.Failure([.. resolution.Errors]);

                currency = card.Currency.Value;
                cardStatement = resolution.Value!.Statement;
                cardStatementCreated = resolution.Value!.Created;
                suggestedStatementId = cardStatement.Id;
            }
            else
            {
                var accountRepo = ctx.AcquireRepository<IAccountRepository>();
                var account = await accountRepo.FindByIdForUserAsync(recurring.AccountId!.Value, input.UserId, token);
                if (account is null) return Result<GeneratedOccurrenceDto>.Failure([AccountErrors.NotFound]);
                currency = account.Currency.Value;
            }

            GeneratedOccurrenceDto dto;

            if (input.Destination == ToInbox)
            {
                var pendingRepo = ctx.AcquireRepository<IPendingTransactionRepository>();

                // The inbox enforces one suggestion per recurrence + date (uq_fin011). Surface a clean
                // conflict instead of letting the unique-index violation bubble up as a 500.
                if (await pendingRepo.ExistsForRecurrenceOnDateAsync(recurring.Id, occurredOn, token))
                    return Result<GeneratedOccurrenceDto>.Failure([RecurringTransactionErrors.OccurrenceAlreadyInInbox]);

                var payload = SerializePayload(recurring, occurredOn, currency, amount, description, payee, systemCategoryId, userCategoryId);

                var pending = PendingTransaction.CreateFromRecurrence(
                    input.UserId,
                    recurring.Id,
                    recurring.AccountId,
                    recurring.CardId,
                    recurring.Kind,
                    amount,
                    currency,
                    occurredOn,
                    description,
                    payee,
                    systemCategoryId,
                    userCategoryId,
                    suggestedStatementId,
                    payload,
                    timeProvider);

                await pendingRepo.AddAsync(pending, token);
                await ctx.RecordAsync(input.UserId, input.UserId, PendingTransactionEvents.EntityType, pending.Id,
                    PendingTransactionEvents.Created, now, new { source = "manual-recurrence", recurringTransactionId = recurring.Id, date = occurredOn }, ct: token);
                dto = new GeneratedOccurrenceDto(ToInbox, PendingTransactionDto.From(pending), null);
            }
            else
            {
                if (amount is null) return Result<GeneratedOccurrenceDto>.Failure([RecurringTransactionErrors.ManualGenerationRequiresAmount]);

                var txRepo = ctx.AcquireRepository<ITransactionRepository>();
                Transaction tx;

                if (targetIsCard)
                {
                    tx = Transaction.CreateStatementTransaction(
                        input.UserId,
                        recurring.CardId!.Value,
                        cardStatement!.Id,
                        kind,
                        CurrencyCode.Create(currency),
                        amount.Value,
                        occurredOn,
                        description,
                        payee,
                        input.Notes,
                        systemCategoryId,
                        userCategoryId,
                        timeProvider);

                    cardStatement.SyncAmounts(
                        cardStatement.TotalAmount + amount.Value * kind.StatementSign,
                        cardStatement.PaidAmount,
                        DateOnly.FromDateTime(now.UtcDateTime),
                        timeProvider);
                    // A freshly-created statement is already tracked as Added; calling UpdateAsync on it
                    // would flip its state to Modified and emit an UPDATE instead of the INSERT, leaving
                    // the transaction's FK dangling. Only persist an explicit update for pre-existing ones.
                    if (!cardStatementCreated)
                    {
                        var statementRepo = ctx.AcquireRepository<ICardStatementRepository>();
                        await statementRepo.UpdateAsync(cardStatement, token);
                    }
                }
                else
                {
                    tx = Transaction.CreateAccountTransaction(
                        input.UserId,
                        recurring.AccountId!.Value,
                        kind,
                        CurrencyCode.Create(currency),
                        amount.Value,
                        occurredOn,
                        description,
                        payee,
                        input.Notes,
                        systemCategoryId,
                        userCategoryId,
                        post: true,
                        timeProvider);
                }

                tx.MarkAsRecurrence(recurring.Id, pendingTransactionId: null);
                await txRepo.AddAsync(tx, token);
                await ctx.RecordAsync(input.UserId, input.UserId, TransactionEvents.EntityType, tx.Id,
                    TransactionEvents.Created, now, new { origin = "recurrence", source = "manual", recurringTransactionId = recurring.Id }, ct: token);
                dto = new GeneratedOccurrenceDto(ToTransactions, null, TransactionDto.From(tx));
            }

            // Optionally advance the schedule from the scheduled date (not the possibly-overridden
            // occurredOn). When skipped, the recurrence is untouched so the user can generate several
            // records for the same occurrence without consuming the schedule.
            if (input.AdvanceSchedule)
            {
                recurring.AdvanceCursor(scheduledDate);
                await recurringRepo.UpdateAsync(recurring, token);
                if (recurring.IsFinished)
                    await ctx.RecordAsync(input.UserId, input.UserId, RecurringTransactionEvents.EntityType, recurring.Id,
                        RecurringTransactionEvents.Finished, now, ct: token);
            }

            await ctx.RecordAsync(input.UserId, input.UserId, RecurringTransactionEvents.EntityType, recurring.Id,
                RecurringTransactionEvents.OccurrenceGenerated, now,
                new { date = scheduledDate, destination = input.Destination, manual = true, advancedSchedule = input.AdvanceSchedule }, ct: token);

            return Result<GeneratedOccurrenceDto>.Success(dto);
        }, cancellationToken: ct);

        return result.IsFailure ? Fail([.. result.Errors]) : Ok(result.Value!);
    }

    /// <summary>Serializes the generated occurrence as the immutable original-suggestion snapshot.</summary>
    private static string SerializePayload(
        RecurringTransaction r, DateOnly occurredOn, string currency, decimal? amount,
        string description, string? payee, Guid? systemCategoryId, Guid? userCategoryId) =>
        JsonSerializer.Serialize(new
        {
            r.AccountId,
            r.CardId,
            r.Kind,
            Amount = amount,
            Currency = currency,
            OccurredOn = occurredOn,
            Description = description,
            Payee = payee,
            SystemCategoryId = systemCategoryId,
            UserCategoryId = userCategoryId
        });
}
