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
        if (kind.IsTransferLeg)
            return Fail(TransactionErrors.TransferLegNotAllowed);

        var hasAccount = input.AccountId is not null;
        var hasCard = input.CardId is not null;
        if (hasAccount == hasCard)
            return Fail(StatementErrors.InvalidTarget);

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

                if (kind.IsStatementPayment)
                    return Result<Transaction>.Failure([TransactionErrors.InvalidKind(kind.Value)]);

                var post = input.OccurredOn <= today;
                var transaction = Transaction.CreateAccountTransaction(
                    input.UserId, account.Id, kind, account.Currency, input.Amount, input.OccurredOn,
                    input.Description, input.Payee, input.Notes, input.SystemCategoryId, input.UserCategoryId,
                    post, timeProvider);

                await transactions.AddAsync(transaction, token);
                await ctx.RecordAsync(
                    input.UserId, input.UserId, "transaction", transaction.Id, "transaction.created", now,
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

        if (!kind.CanTargetStatement)
            return Fail(TransactionErrors.InvalidKind(kind.Value));

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
            statement.SyncAmounts(
                statement.TotalAmount + (input.Amount * kind.StatementSign),
                statement.PaidAmount,
                today,
                timeProvider);
            await statements.UpdateAsync(statement, token);
            await ctx.RecordAsync(
                input.UserId, input.UserId, "transaction", transaction.Id, "transaction.created", now,
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
}
