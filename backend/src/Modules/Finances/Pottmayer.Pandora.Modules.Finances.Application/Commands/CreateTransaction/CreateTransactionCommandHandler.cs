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
using Pottmayer.Tars.Data.Abstractions.DataContext;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Finances.Application.Commands.CreateTransaction;

public sealed class CreateTransactionCommandHandler(
    IUnitOfWorkFactory factory,
    IStatementResolver statementResolver,
    TimeProvider timeProvider)
    : CommandHandlerBase<CreateTransactionCommand, TransactionDto>
{
    protected override async Task<Result<TransactionDto>> HandleAsync(
        CreateTransactionCommand request, CancellationToken ct)
    {
        var input = request.Input;
        var now = timeProvider.GetUtcNow();
        var today = DateOnly.FromDateTime(now.UtcDateTime);

        if (string.IsNullOrWhiteSpace(input.Description))
            return Fail(TransactionErrors.InvalidDescription);

        if (input.Amount <= 0)
            return Fail(TransactionErrors.InvalidAmount);

        if (!TransactionKind.IsSupported(input.Kind))
            return Fail(TransactionErrors.InvalidKind(input.Kind));

        var kind = TransactionKind.FromValue(input.Kind);
        // transfer-in/transfer-out only ever come in pairs from CreateTransfer; a lone leg here
        // would have no matching counterpart.
        if (kind.IsTransferLeg)
            return Fail(TransactionErrors.TransferLegNotAllowed);

        // Exactly one destination: an account movement and a card statement movement are mutually exclusive.
        var hasAccount = input.AccountId is not null;
        var hasCard = input.CardId is not null;
        if (hasAccount == hasCard)
            return Fail(StatementErrors.InvalidTarget);

        if (input.Installments < 1)
            return Fail(InstallmentErrors.InvalidCount);

        if (input.Installments > 1)
        {
            // Installments are a card-only, expense-only concept in this phase.
            if (!hasCard)
                return Fail(InstallmentErrors.RequiresCard);
            if (kind != TransactionKind.Expense)
                return Fail(InstallmentErrors.RequiresExpenseKind);

            return await CreateInstallmentPurchaseAsync(input, now, today, ct);
        }

        if (input.AccountId is not null)
        {
            var result = await factory.ExecuteAsync(FinancesModule.Name, async (ctx, token) =>
            {
                var transactions = ctx.AcquireRepository<ITransactionRepository>();
                var accounts = ctx.AcquireRepository<IAccountRepository>();
                var account = await accounts.FindByIdForUserAsync(input.AccountId.Value, input.UserId, token);
                if (account is null)
                    return Result<Transaction>.Failure([AccountErrors.NotFound]);

                if (account.IsArchived)
                    return Result<Transaction>.Failure([TransactionErrors.AccountArchived]);

                if (kind.RequiresInvestmentAccount && account.Type != AccountType.Investment)
                    return Result<Transaction>.Failure([TransactionErrors.KindRequiresInvestmentAccount(kind.Value)]);

                // Statement payments are created exclusively through PayStatement, which also
                // updates the statement's balance — never as a generic account transaction.
                if (kind.IsStatementPayment)
                    return Result<Transaction>.Failure([TransactionErrors.InvalidKind(kind.Value)]);

                // A future-dated entry is scheduled as pending rather than posted immediately.
                var post = input.OccurredOn <= today;
                var transaction = Transaction.CreateAccountTransaction(
                    input.UserId, account.Id, kind, account.Currency, input.Amount, input.OccurredOn,
                    input.Description, input.Payee, input.Notes, input.SystemCategoryId, input.UserCategoryId,
                    post, timeProvider);

                await transactions.AddAsync(transaction, token);
                await ctx.RecordAsync(
                    input.UserId, input.UserId, TransactionEvents.EntityType, transaction.Id, TransactionEvents.Created, now,
                    new
                    {
                        accountId = transaction.AccountId,
                        kind = transaction.Kind.Value,
                        status = transaction.Status.Value,
                        amount = transaction.Amount,
                        currency = transaction.Currency.Value,
                        occurredOn = transaction.OccurredOn
                    },
                    ct: token);

                return Result<Transaction>.Success(transaction);
            }, cancellationToken: ct);

            return result.IsFailure ? Fail([.. result.Errors]) : Ok(TransactionDto.From(result.Value!));
        }

        // Only expense/refund kinds make sense as a standalone card statement movement; everything
        // else (income, transfers, investment kinds) requires an account.
        if (!kind.CanTargetStatement)
            return Fail(TransactionErrors.InvalidKind(kind.Value));

        // Resolving/creating the statement runs in its own unit of work, separate from the
        // transaction write below, so its "statement.created" event (if any) is recorded up front.
        var statementResult = await factory.ExecuteAsync(FinancesModule.Name, async (ctx, token) =>
        {
            var cards = ctx.AcquireRepository<ICardRepository>();
            var statements = ctx.AcquireRepository<ICardStatementRepository>();
            var card = await cards.FindByIdForUserAsync(input.CardId!.Value, input.UserId, token);
            if (card is null)
                return Result<(Card Card, StatementMaintenance.StatementResolutionResult Statement)>.Failure([CardErrors.NotFound]);
            if (card.IsArchived)
                return Result<(Card Card, StatementMaintenance.StatementResolutionResult Statement)>.Failure([CardErrors.Archived]);

            var ensured = await StatementMaintenance.EnsureStatementForPurchaseAsync(
                statements, statementResolver, card, input.UserId, input.OccurredOn, input.CardStatementId, timeProvider, token);
            if (ensured.IsFailure)
                return Result<(Card Card, StatementMaintenance.StatementResolutionResult Statement)>.Failure([.. ensured.Errors]);

            var resolved = ensured.Value!;
            if (resolved.Created)
            {
                await ctx.RecordAsync(
                    input.UserId, input.UserId, StatementEvents.EntityType, resolved.Statement.Id, StatementEvents.Created, now,
                    new
                    {
                        resolved.Statement.CardId,
                        resolved.Statement.ReferenceMonth,
                        resolved.Statement.ClosingDate,
                        resolved.Statement.DueDate
                    },
                    ct: token);
            }

            return Result<(Card Card, StatementMaintenance.StatementResolutionResult Statement)>.Success((card, resolved));
        }, cancellationToken: ct);

        if (statementResult.IsFailure)
            return Fail([.. statementResult.Errors]);

        var prepared = statementResult.Value;
        var cardContext = prepared.Card;
        var statementContext = prepared.Statement.Statement;

        var transactionResult = await factory.ExecuteAsync(FinancesModule.Name, async (ctx, token) =>
        {
            var statements = ctx.AcquireRepository<ICardStatementRepository>();
            var transactions = ctx.AcquireRepository<ITransactionRepository>();
            var statement = await statements.FindByIdForUserAsync(statementContext.Id, input.UserId, token);
            if (statement is null)
                return Result<Transaction>.Failure([StatementErrors.NotFound]);

            var transaction = Transaction.CreateStatementTransaction(
                input.UserId,
                cardContext.Id,
                statement.Id,
                kind,
                cardContext.Currency,
                input.Amount,
                input.OccurredOn,
                input.Description,
                input.Payee,
                input.Notes,
                input.SystemCategoryId,
                input.UserCategoryId,
                timeProvider);

            await transactions.AddAsync(transaction, token);
            // StatementSign flips the contribution for a refund vs. an expense.
            StatementAmountSync.Apply(statement, input.Amount * kind.StatementSign, 0m, today, timeProvider);
            await statements.UpdateAsync(statement, token);
            await ctx.RecordAsync(
                input.UserId, input.UserId, TransactionEvents.EntityType, transaction.Id, TransactionEvents.Created, now,
                new
                {
                    cardId = cardContext.Id,
                    cardStatementId = statement.Id,
                    kind = transaction.Kind.Value,
                    status = transaction.Status.Value,
                    amount = transaction.Amount,
                    currency = transaction.Currency.Value,
                    occurredOn = transaction.OccurredOn
                },
                ct: token);

            return Result<Transaction>.Success(transaction);
        }, cancellationToken: ct);

        return transactionResult.IsFailure
            ? Fail([.. transactionResult.Errors])
            : Ok(TransactionDto.From(transactionResult.Value!));
    }

    /// <summary>
    /// A card purchase split into N installments: one <see cref="InstallmentPlan"/> plus N posted
    /// installment transactions, one on each consecutive statement (created on demand). The whole
    /// thing — plan, transactions and every affected statement total — commits in a single database
    /// transaction so the ledger is never partially written. Returns the first installment.
    /// </summary>
    private async Task<Result<TransactionDto>> CreateInstallmentPurchaseAsync(
        CreateTransactionInput input, DateTimeOffset now, DateOnly today, CancellationToken ct)
    {
        var count = input.Installments;

        var result = await factory.ExecuteAsync(FinancesModule.Name, async (ctx, token) =>
        {
            var cards = ctx.AcquireRepository<ICardRepository>();
            var statements = ctx.AcquireRepository<ICardStatementRepository>();
            var transactions = ctx.AcquireRepository<ITransactionRepository>();
            var plans = ctx.AcquireRepository<IInstallmentPlanRepository>();

            var card = await cards.FindByIdForUserAsync(input.CardId!.Value, input.UserId, token);
            if (card is null)
                return Result<Transaction>.Failure([CardErrors.NotFound]);
            if (card.IsArchived)
                return Result<Transaction>.Failure([CardErrors.Archived]);

            // Groups every event this purchase generates (plan, statements, installments) so the
            // user can trace the whole operation as one unit in the audit trail.
            var correlationId = Guid.CreateVersion7();

            var ensured = await StatementMaintenance.EnsureStatementForPurchaseAsync(
                statements, statementResolver, card, input.UserId, input.OccurredOn, input.CardStatementId, timeProvider, token);
            if (ensured.IsFailure)
                return Result<Transaction>.Failure([.. ensured.Errors]);

            var firstStatement = ensured.Value!.Statement;
            var firstCreated = ensured.Value!.Created;
            if (firstCreated)
                await RecordStatementCreatedAsync(ctx, input.UserId, firstStatement, now, correlationId, token);

            var firstMonth = ParseFirstOfMonth(firstStatement.ReferenceMonth);

            var plan = InstallmentPlan.CreateManual(
                input.UserId, card.Id, input.Amount, count, firstStatement.ReferenceMonth, input.Description, timeProvider);
            await plans.AddAsync(plan, token);
            await ctx.RecordAsync(
                input.UserId, input.UserId, InstallmentPlanEvents.EntityType, plan.Id, InstallmentPlanEvents.Created, now,
                new { plan.CardId, plan.TotalAmount, plan.InstallmentCount, plan.FirstReferenceMonth, plan.NormalizedDescription },
                correlationId, token);

            // Cents-accurate split: any rounding remainder lands on the first installment so the
            // parts always sum back to the original purchase amount exactly.
            var parts = InstallmentPlan.SplitAmount(input.Amount, count);
            Transaction? firstInstallment = null;

            for (var i = 1; i <= count; i++)
            {
                CardStatement statement;
                bool created;
                if (i == 1)
                {
                    // The first installment reuses the statement already resolved above.
                    statement = firstStatement;
                    created = firstCreated;
                }
                else
                {
                    // Each later installment lands on the statement N months after the first,
                    // anchored to the card's own closing day so it always picks the right cycle.
                    var anchorMonth = firstMonth.AddMonths(i - 1);
                    var anchor = new DateOnly(anchorMonth.Year, anchorMonth.Month, card.ClosingDay);
                    var ensuredI = await StatementMaintenance.EnsureExactStatementAsync(
                        statements, statementResolver, card, input.UserId, anchor, timeProvider, token);
                    statement = ensuredI.Value!.Statement;
                    created = ensuredI.Value!.Created;
                    if (created)
                        await RecordStatementCreatedAsync(ctx, input.UserId, statement, now, correlationId, token);
                }

                var amount = parts[i - 1];
                var tx = Transaction.CreateInstallmentTransaction(
                    input.UserId, card.Id, statement.Id, plan.Id, (short)i, card.Currency, amount, input.OccurredOn,
                    input.Description, input.Payee, input.Notes, input.SystemCategoryId, input.UserCategoryId, timeProvider);
                await transactions.AddAsync(tx, token);

                // Expense raises the statement total; recompute the cache in this same transaction (D1).
                StatementAmountSync.Apply(statement, amount, 0m, today, timeProvider);
                // A statement just created above is added (not updated) by EnsureExactStatementAsync
                // itself, so only a pre-existing statement needs an explicit update here.
                if (!created)
                    await statements.UpdateAsync(statement, token);

                await ctx.RecordAsync(
                    input.UserId, input.UserId, TransactionEvents.EntityType, tx.Id, TransactionEvents.Created, now,
                    new
                    {
                        cardId = card.Id,
                        cardStatementId = statement.Id,
                        installmentPlanId = plan.Id,
                        installmentNumber = i,
                        kind = tx.Kind.Value,
                        amount = tx.Amount,
                        currency = tx.Currency.Value,
                        occurredOn = tx.OccurredOn
                    }, correlationId, token);

                firstInstallment ??= tx;
            }

            return Result<Transaction>.Success(firstInstallment!);
        }, cancellationToken: ct);

        return result.IsFailure ? Fail([.. result.Errors]) : Ok(TransactionDto.From(result.Value!));
    }

    /// <summary>Shared "statement.created" audit event for any statement opened on demand while purchasing.</summary>
    private static Task RecordStatementCreatedAsync(
        IDataContext ctx,
        Guid userId, CardStatement statement, DateTimeOffset now, Guid correlationId, CancellationToken ct) =>
        ctx.RecordAsync(
            userId, userId, StatementEvents.EntityType, statement.Id, StatementEvents.Created, now,
            new { statement.CardId, statement.ReferenceMonth, statement.ClosingDate, statement.DueDate },
            correlationId, ct);

    /// <summary>Parses a <c>yyyy-MM</c> reference month into its first calendar day.</summary>
    private static DateOnly ParseFirstOfMonth(string referenceMonth)
    {
        var year = int.Parse(referenceMonth[..4]);
        var month = int.Parse(referenceMonth[5..7]);
        return new DateOnly(year, month, 1);
    }
}
