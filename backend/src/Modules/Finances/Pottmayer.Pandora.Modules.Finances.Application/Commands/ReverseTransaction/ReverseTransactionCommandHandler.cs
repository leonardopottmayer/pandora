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

namespace Pottmayer.Pandora.Modules.Finances.Application.Commands.ReverseTransaction;

/// <summary>
/// Generalizes <c>refund</c> to any posted transaction: creates a new, opposite-effect transaction
/// dated today and linked to the original via <see cref="Transaction.ReversedTransactionId"/>. The
/// original is never modified — append-only, per the module's reversibility design.
/// </summary>
public sealed class ReverseTransactionCommandHandler(
    IUnitOfWorkFactory factory,
    IStatementResolver statementResolver,
    TimeProvider timeProvider)
    : CommandHandlerBase<ReverseTransactionCommand, TransactionDto>
{
    protected override async Task<Result<TransactionDto>> HandleAsync(
        ReverseTransactionCommand request, CancellationToken ct)
    {
        var input = request.Input;
        var now = timeProvider.GetUtcNow();
        var today = DateOnly.FromDateTime(now.UtcDateTime);

        var loadResult = await factory.ExecuteAsync(FinancesModule.Name, async (ctx, token) =>
        {
            var transactions = ctx.AcquireRepository<ITransactionRepository>();

            var original = await transactions.FindByIdForUserAsync(input.TransactionId, input.UserId, token);
            if (original is null)
                return Result<Transaction>.Failure([TransactionErrors.NotFound]);

            if (!original.IsPosted)
                return Result<Transaction>.Failure([TransactionErrors.NotPosted]);

            if (await transactions.ExistsReversalForAsync(original.Id, input.UserId, token))
                return Result<Transaction>.Failure([TransactionErrors.AlreadyReversed]);

            return Result<Transaction>.Success(original);
        }, cancellationToken: ct);

        if (loadResult.IsFailure)
            return Fail([.. loadResult.Errors]);

        var original = loadResult.Value!;

        if (original.InstallmentPlanId is not null)
            return Fail(TransactionErrors.ReversalNotSupported(original.Kind.Value));

        if (original.TransferGroupId is not null)
            return await ReverseTransferAsync(original, input, now, today, ct);

        if (original.PaidStatementId is not null)
            return await ReverseStatementPaymentAsync(original, input, now, today, ct);

        if (original.CardStatementId is not null)
            return await ReverseCardStatementTransactionAsync(original, input, now, today, ct);

        var reversalKind = original.Kind.ReversalKind(targetsStatement: false);
        if (reversalKind is null)
            return Fail(TransactionErrors.ReversalNotSupported(original.Kind.Value));

        return await ReverseAccountTransactionAsync(original, reversalKind, input, now, today, ct);
    }

    /// <summary>Plain account entry (income/expense/investment-*): a same-account, opposite-kind mirror.</summary>
    private async Task<Result<TransactionDto>> ReverseAccountTransactionAsync(
        Transaction original, TransactionKind reversalKind, ReverseTransactionInput input, DateTimeOffset now, DateOnly today, CancellationToken ct)
    {
        var result = await factory.ExecuteAsync(FinancesModule.Name, async (ctx, token) =>
        {
            var transactions = ctx.AcquireRepository<ITransactionRepository>();

            var reversal = Transaction.CreateAccountTransaction(
                input.UserId, original.AccountId!.Value, reversalKind, original.Currency, original.Amount, today,
                DescriptionFor(input, original), original.Payee, original.Notes, original.SystemCategoryId, original.UserCategoryId,
                post: true, timeProvider);
            reversal.MarkAsReversal(original.Id);

            await transactions.AddAsync(reversal, token);
            await RecordReversalAuditAsync(ctx, original, reversal, input.UserId, now, token);

            return Result<Transaction>.Success(reversal);
        }, cancellationToken: ct);

        return result.IsFailure ? Fail([.. result.Errors]) : Ok(TransactionDto.From(result.Value!));
    }

    /// <summary>
    /// Transfer pair: creates a new pair flowing in the opposite direction, with a new
    /// <see cref="Transaction.TransferGroupId"/>. Each new leg links back to the original leg on the
    /// same account.
    /// </summary>
    private async Task<Result<TransactionDto>> ReverseTransferAsync(
        Transaction original, ReverseTransactionInput input, DateTimeOffset now, DateOnly today, CancellationToken ct)
    {
        var result = await factory.ExecuteAsync(FinancesModule.Name, async (ctx, token) =>
        {
            var transactions = ctx.AcquireRepository<ITransactionRepository>();

            var legs = await transactions.GetByTransferGroupAsync(original.TransferGroupId!.Value, input.UserId, token);
            var outLeg = legs.Single(l => l.Kind == TransactionKind.TransferOut);
            var inLeg = legs.Single(l => l.Kind == TransactionKind.TransferIn);

            var description = DescriptionFor(input, outLeg);

            // The new pair flows from the original destination back to the original source.
            var (newOut, newIn) = Transaction.CreateTransferPair(
                input.UserId,
                fromAccountId: inLeg.AccountId!.Value, fromCurrency: inLeg.Currency, amountOut: inLeg.Amount,
                toAccountId: outLeg.AccountId!.Value, toCurrency: outLeg.Currency, amountIn: outLeg.Amount,
                fxRate: outLeg.FxRate, today, description, original.Notes, timeProvider);

            newOut.MarkAsReversal(inLeg.Id);
            newIn.MarkAsReversal(outLeg.Id);

            await transactions.AddAsync(newOut, token);
            await transactions.AddAsync(newIn, token);

            await RecordReversalAuditAsync(ctx, outLeg, newIn, input.UserId, now, token);
            await RecordReversalAuditAsync(ctx, inLeg, newOut, input.UserId, now, token);

            // Return the new leg that lives on the same account as the requested transaction.
            var reversal = original.Kind == TransactionKind.TransferOut ? newIn : newOut;
            return Result<Transaction>.Success(reversal);
        }, cancellationToken: ct);

        return result.IsFailure ? Fail([.. result.Errors]) : Ok(TransactionDto.From(result.Value!));
    }

    /// <summary>
    /// Card-statement payment: refunds the money to the paying account and reduces the paid amount
    /// of the statement that was paid, as it stands today (may go negative — accepted, same rule as
    /// <c>unvoid</c>).
    /// </summary>
    private async Task<Result<TransactionDto>> ReverseStatementPaymentAsync(
        Transaction original, ReverseTransactionInput input, DateTimeOffset now, DateOnly today, CancellationToken ct)
    {
        var result = await factory.ExecuteAsync(FinancesModule.Name, async (ctx, token) =>
        {
            var transactions = ctx.AcquireRepository<ITransactionRepository>();
            var statements = ctx.AcquireRepository<ICardStatementRepository>();

            var statement = await statements.FindByIdForUserAsync(original.PaidStatementId!.Value, input.UserId, token);
            if (statement is null)
                return Result<Transaction>.Failure([StatementErrors.NotFound]);

            var reversal = Transaction.CreateAccountTransaction(
                input.UserId, original.AccountId!.Value, TransactionKind.Refund, original.Currency, original.Amount, today,
                DescriptionFor(input, original), original.Payee, original.Notes, original.SystemCategoryId, original.UserCategoryId,
                post: true, timeProvider);
            reversal.MarkAsReversal(original.Id);

            await transactions.AddAsync(reversal, token);

            StatementAmountSync.Apply(statement, 0m, -original.Amount, today, timeProvider);
            await statements.UpdateAsync(statement, token);

            await RecordReversalAuditAsync(ctx, original, reversal, input.UserId, now, token);

            return Result<Transaction>.Success(reversal);
        }, cancellationToken: ct);

        return result.IsFailure ? Fail([.. result.Errors]) : Ok(TransactionDto.From(result.Value!));
    }

    /// <summary>
    /// Standalone card purchase/refund: the mirror entry lands on the <em>current</em> open statement
    /// (resolved/created just like a new purchase), not the original — mirroring how a real refund
    /// shows up on this month's statement regardless of when the original purchase was billed.
    /// </summary>
    private async Task<Result<TransactionDto>> ReverseCardStatementTransactionAsync(
        Transaction original, ReverseTransactionInput input, DateTimeOffset now, DateOnly today, CancellationToken ct)
    {
        var reversalKind = original.Kind.ReversalKind(targetsStatement: true);
        if (reversalKind is null)
            return Fail(TransactionErrors.ReversalNotSupported(original.Kind.Value));

        var statementResult = await factory.ExecuteAsync(FinancesModule.Name, async (ctx, token) =>
        {
            var cards = ctx.AcquireRepository<ICardRepository>();
            var statements = ctx.AcquireRepository<ICardStatementRepository>();

            var card = await cards.FindByIdForUserAsync(original.CardId!.Value, input.UserId, token);
            if (card is null)
                return Result<Guid>.Failure([CardErrors.NotFound]);

            var ensured = await StatementMaintenance.EnsureStatementForPurchaseAsync(
                statements, statementResolver, card, input.UserId, today, forcedStatementId: null, timeProvider, token);
            if (ensured.IsFailure)
                return Result<Guid>.Failure([.. ensured.Errors]);

            var resolved = ensured.Value!;
            if (resolved.Created)
            {
                await ctx.RecordAsync(
                    input.UserId, input.UserId, "statement", resolved.Statement.Id, "statement.created", now,
                    new
                    {
                        resolved.Statement.CardId,
                        resolved.Statement.ReferenceMonth,
                        resolved.Statement.ClosingDate,
                        resolved.Statement.DueDate
                    },
                    ct: token);
            }

            return Result<Guid>.Success(resolved.Statement.Id);
        }, cancellationToken: ct);

        if (statementResult.IsFailure)
            return Fail([.. statementResult.Errors]);

        var statementId = statementResult.Value!;

        var result = await factory.ExecuteAsync(FinancesModule.Name, async (ctx, token) =>
        {
            var transactions = ctx.AcquireRepository<ITransactionRepository>();
            var statements = ctx.AcquireRepository<ICardStatementRepository>();

            var statement = await statements.FindByIdForUserAsync(statementId, input.UserId, token);
            if (statement is null)
                return Result<Transaction>.Failure([StatementErrors.NotFound]);

            var reversal = Transaction.CreateStatementTransaction(
                input.UserId, original.CardId!.Value, statement.Id, reversalKind, original.Currency, original.Amount, today,
                DescriptionFor(input, original), original.Payee, original.Notes, original.SystemCategoryId, original.UserCategoryId,
                timeProvider);
            reversal.MarkAsReversal(original.Id);

            await transactions.AddAsync(reversal, token);
            StatementAmountSync.Apply(statement, original.Amount * reversalKind.StatementSign, 0m, today, timeProvider);
            await statements.UpdateAsync(statement, token);

            await RecordReversalAuditAsync(ctx, original, reversal, input.UserId, now, token);

            return Result<Transaction>.Success(reversal);
        }, cancellationToken: ct);

        return result.IsFailure ? Fail([.. result.Errors]) : Ok(TransactionDto.From(result.Value!));
    }

    private static string DescriptionFor(ReverseTransactionInput input, Transaction original) =>
        string.IsNullOrWhiteSpace(input.Description) ? $"Estorno: {original.Description}" : input.Description;

    private static async Task RecordReversalAuditAsync(
        IDataContext ctx, Transaction original, Transaction reversal, Guid userId, DateTimeOffset now, CancellationToken ct)
    {
        var correlationId = Guid.CreateVersion7();

        await ctx.RecordAsync(
            userId, userId, "transaction", original.Id, "transaction.reversed", now,
            new { reversalTransactionId = reversal.Id }, correlationId, ct);

        await ctx.RecordAsync(
            userId, userId, "transaction", reversal.Id, "transaction.created", now,
            new
            {
                accountId = reversal.AccountId,
                cardStatementId = reversal.CardStatementId,
                cardId = reversal.CardId,
                kind = reversal.Kind.Value,
                status = reversal.Status.Value,
                amount = reversal.Amount,
                currency = reversal.Currency.Value,
                occurredOn = reversal.OccurredOn,
                origin = reversal.Origin,
                reversedTransactionId = original.Id
            },
            correlationId, ct);
    }
}
