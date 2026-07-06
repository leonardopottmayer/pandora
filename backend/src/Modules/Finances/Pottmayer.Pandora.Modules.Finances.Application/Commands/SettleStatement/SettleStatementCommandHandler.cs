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

namespace Pottmayer.Pandora.Modules.Finances.Application.Commands.SettleStatement;

public sealed class SettleStatementCommandHandler(IUnitOfWorkFactory factory, TimeProvider timeProvider)
    : CommandHandlerBase<SettleStatementCommand, CardStatementDto>
{
    protected override async Task<Result<CardStatementDto>> HandleAsync(SettleStatementCommand request, CancellationToken ct)
    {
        var input = request.Input;
        var now = timeProvider.GetUtcNow();
        var today = DateOnly.FromDateTime(now.UtcDateTime);

        var result = await factory.ExecuteAsync(FinancesModule.Name, async (ctx, token) =>
        {
            var statements = ctx.AcquireRepository<ICardStatementRepository>();
            var transactions = ctx.AcquireRepository<ITransactionRepository>();

            var statement = await statements.FindByIdForUserAsync(input.StatementId, input.UserId, token);
            if (statement is null)
                return Result<CardStatement>.Failure([StatementErrors.NotFound]);

            var amount = statement.RemainingAmount;
            if (amount <= 0m)
                return Result<CardStatement>.Failure([StatementErrors.NothingToSettle]);

            var card = await ctx.AcquireRepository<ICardRepository>().FindByIdForUserAsync(statement.CardId, input.UserId, token);
            if (card is null)
                return Result<CardStatement>.Failure([CardErrors.NotFound]);

            var occurredOn = input.OccurredOn ?? today;
            var writeoff = Transaction.CreateStatementWriteoff(
                input.UserId,
                statement.Id,
                card.Currency,
                amount,
                occurredOn,
                description: "",
                input.Notes,
                timeProvider,
                systemDescription: SystemDescription.StatementWriteoff(statement.ReferenceMonth));

            await transactions.AddAsync(writeoff, token);
            // Applies the write-off to the paid side of the balance; with the full remaining amount
            // this drives the statement straight to 'paid'.
            StatementAmountSync.Apply(statement, 0m, amount, today, timeProvider);
            await statements.UpdateAsync(statement, token);

            await ctx.RecordAsync(
                input.UserId, input.UserId, TransactionEvents.EntityType, writeoff.Id, TransactionEvents.Created, now,
                new
                {
                    kind = writeoff.Kind.Value,
                    status = writeoff.Status.Value,
                    amount = writeoff.Amount,
                    currency = writeoff.Currency.Value,
                    occurredOn = writeoff.OccurredOn
                },
                ct: token);

            await ctx.RecordAsync(input.UserId, input.UserId, StatementEvents.EntityType, statement.Id,
                StatementEvents.SettledWithoutCash, now, new { amount }, ct: token);

            return Result<CardStatement>.Success(statement);
        }, cancellationToken: ct);

        return result.IsFailure ? Fail([.. result.Errors]) : Ok(CardStatementDto.From(result.Value!));
    }
}
