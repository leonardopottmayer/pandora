using Pottmayer.Pandora.Modules.Finances.Abstractions;
using Pottmayer.Pandora.Modules.Finances.Application.Auditing;
using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Pandora.Modules.Finances.Application.Services;
using Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Finances.Domain.Errors;
using Pottmayer.Pandora.Modules.Finances.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Finances.Domain.ValueObjects;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Finances.Application.Commands.PayStatement;

public sealed class PayStatementCommandHandler(IUnitOfWorkFactory factory, TimeProvider timeProvider)
    : CommandHandlerBase<PayStatementCommand, CardStatementDto>
{
    protected override async Task<Result<CardStatementDto>> HandleAsync(PayStatementCommand request, CancellationToken ct)
    {
        var input = request.Input;
        if (input.Amount <= 0m)
            return Fail(StatementErrors.InvalidPaymentAmount);

        var now = timeProvider.GetUtcNow();
        var today = DateOnly.FromDateTime(now.UtcDateTime);

        var result = await factory.ExecuteAsync(FinancesModule.Name, async (ctx, token) =>
        {
            var statements = ctx.AcquireRepository<ICardStatementRepository>();
            var accounts = ctx.AcquireRepository<IAccountRepository>();
            var transactions = ctx.AcquireRepository<ITransactionRepository>();
            var categories = ctx.AcquireRepository<ISystemCategoryReader>();

            var statement = await statements.FindByIdForUserAsync(input.StatementId, input.UserId, token);
            if (statement is null)
                return Result<CardStatement>.Failure([StatementErrors.NotFound]);

            var account = await accounts.FindByIdForUserAsync(input.AccountId, input.UserId, token);
            if (account is null)
                return Result<CardStatement>.Failure([AccountErrors.NotFound]);
            if (account.IsArchived)
                return Result<CardStatement>.Failure([StatementErrors.CannotPayWithArchivedAccount]);

            var card = await ctx.AcquireRepository<ICardRepository>().FindByIdForUserAsync(statement.CardId, input.UserId, token);
            if (card is null)
                return Result<CardStatement>.Failure([CardErrors.NotFound]);
            // Same-currency payments don't need a rate; cross-currency ones must supply one explicitly.
            if (account.Currency != card.Currency && input.FxRate is null)
                return Result<CardStatement>.Failure([StatementErrors.MissingFxRate]);

            var occurredOn = input.OccurredOn ?? today;
            // Without a user description, the transaction renders a localized system description
            // (e.g. "Payment — June 2026") built from the statement's reference month at read time.
            var hasUserDescription = !string.IsNullOrWhiteSpace(input.Description);
            var category = await categories.GetByCodeAsync("credit-card-payment", token);
            var payment = Transaction.CreateStatementPayment(
                input.UserId,
                account.Id,
                statement.Id,
                account.Currency,
                input.Amount,
                occurredOn,
                hasUserDescription ? input.Description! : "",
                null,
                input.Notes,
                fxRate: input.FxRate,
                timeProvider,
                systemCategoryId: category?.Id,
                systemDescription: hasUserDescription ? null : SystemDescription.StatementPayment(statement.ReferenceMonth));

            await transactions.AddAsync(payment, token);
            // Applies the payment to the statement's balance and derives its new status (e.g. paid,
            // partially paid) from the updated amounts.
            StatementAmountSync.Apply(statement, 0m, input.Amount, today, timeProvider);
            await statements.UpdateAsync(statement, token);

            await ctx.RecordAsync(
                input.UserId, input.UserId, TransactionEvents.EntityType, payment.Id, TransactionEvents.Created, now,
                new
                {
                    accountId = payment.AccountId,
                    kind = payment.Kind.Value,
                    status = payment.Status.Value,
                    amount = payment.Amount,
                    currency = payment.Currency.Value,
                    occurredOn = payment.OccurredOn
                },
                ct: token);

            await ctx.RecordAsync(input.UserId, input.UserId, StatementEvents.EntityType, statement.Id, StatementEvents.PaymentReceived, now, new
            {
                amount = input.Amount,
                accountId = account.Id
            }, ct: token);

            // Extra event only when this payment was the one that fully settled the statement.
            if (statement.Status.Value == "paid")
                await ctx.RecordAsync(input.UserId, input.UserId, StatementEvents.EntityType, statement.Id, StatementEvents.Paid, now, ct: token);

            return Result<CardStatement>.Success(statement);
        }, cancellationToken: ct);

        return result.IsFailure ? Fail([.. result.Errors]) : Ok(CardStatementDto.From(result.Value!));
    }
}
