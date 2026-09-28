using System.Text.Json;
using Pottmayer.Pandora.Modules.Finances.Abstractions;
using Pottmayer.Pandora.Modules.Finances.Application.Auditing;
using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Finances.Domain.Errors;
using Pottmayer.Pandora.Modules.Finances.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Finances.Domain.ValueObjects;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Finances.Application.Commands.CreatePendingExpense;

public sealed class CreatePendingExpenseCommandHandler(IUnitOfWorkFactory factory, TimeProvider timeProvider)
    : CommandHandlerBase<CreatePendingExpenseCommand, PendingTransactionDto>
{
    protected override async Task<Result<PendingTransactionDto>> HandleAsync(
        CreatePendingExpenseCommand request, CancellationToken ct)
    {
        var input = request.Input;
        if ((input.AccountId is null) == (input.CardId is null))
            return Fail(PendingTransactionErrors.SingleTargetRequired);
        if (input.Amount <= 0)
            return Fail(PendingTransactionErrors.InvalidAmount);
        if (string.IsNullOrWhiteSpace(input.Description))
            return Fail(PendingTransactionErrors.DescriptionRequired);

        var now = timeProvider.GetUtcNow();

        var result = await factory.ExecuteAsync(FinancesModule.DatabaseKey, async (ctx, token) =>
        {
            // The target fixes the currency; an archived one takes no new movements.
            string currency;
            if (input.AccountId is { } accountId)
            {
                var account = await ctx.AcquireRepository<IAccountRepository>().FindByIdForUserAsync(accountId, input.UserId, token);
                if (account is null) return Result<PendingTransaction>.Failure([AccountErrors.NotFound]);
                if (account.IsArchived) return Result<PendingTransaction>.Failure([AccountErrors.Archived]);
                currency = account.Currency.Value;
            }
            else
            {
                var card = await ctx.AcquireRepository<ICardRepository>().FindByIdForUserAsync(input.CardId!.Value, input.UserId, token);
                if (card is null) return Result<PendingTransaction>.Failure([CardErrors.NotFound]);
                if (card.IsArchived) return Result<PendingTransaction>.Failure([CardErrors.Archived]);
                currency = card.Currency.Value;
            }

            var payload = JsonSerializer.Serialize(new
            {
                input.AccountId, input.CardId, input.Amount, currency, input.OccurredOn, input.Description,
            });
            var pending = PendingTransaction.CreateManual(
                input.UserId, input.AccountId, input.CardId, TransactionKind.Expense.Value, input.Amount, currency,
                input.OccurredOn, input.Description, payload, timeProvider);

            var repo = ctx.AcquireRepository<IPendingTransactionRepository>();
            await repo.AddAsync(pending, token);

            await ctx.RecordAsync(input.UserId, input.UserId, PendingTransactionEvents.EntityType, pending.Id,
                PendingTransactionEvents.Created, now, new { source = pending.Source.Value }, ct: token);

            return Result<PendingTransaction>.Success(pending);
        }, cancellationToken: ct);

        return result.IsFailure ? Fail([.. result.Errors]) : Ok(PendingTransactionDto.From(result.Value!));
    }
}
