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

namespace Pottmayer.Pandora.Modules.Finances.Application.Commands.CreateRecurringTransaction;

public sealed class CreateRecurringTransactionCommandHandler(
    IUnitOfWorkFactory factory,
    TimeProvider timeProvider)
    : CommandHandlerBase<CreateRecurringTransactionCommand, RecurringTransactionDto>
{
    protected override async Task<Result<RecurringTransactionDto>> HandleAsync(
        CreateRecurringTransactionCommand request, CancellationToken ct)
    {
        var input = request.Input;

        if (string.IsNullOrWhiteSpace(input.Name))
            return Fail(RecurringTransactionErrors.MissingName);
        if (string.IsNullOrWhiteSpace(input.Description))
            return Fail(RecurringTransactionErrors.MissingDescription);
        if (!RecurrenceRule.IsValidFrequency(input.Frequency))
            return Fail(RecurringTransactionErrors.InvalidFrequency(input.Frequency));
        if (input.Interval < 1)
            return Fail(RecurringTransactionErrors.InvalidInterval);
        if (input.DayOfMonth is not null and (< 1 or > 31))
            return Fail(RecurringTransactionErrors.InvalidDayOfMonth);
        if (input.Weekday is not null and (< 0 or > 6))
            return Fail(RecurringTransactionErrors.InvalidWeekday);
        if (input.EndDate.HasValue && input.EndDate.Value <= input.StartDate)
            return Fail(RecurringTransactionErrors.EndDateBeforeStart);
        if (input.AutoPost && input.AccountId is not null && input.Amount is null)
            return Fail(RecurringTransactionErrors.AutoPostRequiresAmount);
        if (!TransactionKind.IsSupported(input.Kind))
            return Fail(TransactionErrors.InvalidKind(input.Kind));

        var now = timeProvider.GetUtcNow();

        var result = await factory.ExecuteAsync(FinancesModule.Name, async (ctx, token) =>
        {
            var accounts = ctx.AcquireRepository<IAccountRepository>();
            var cards = ctx.AcquireRepository<ICardRepository>();

            if (input.AccountId is not null)
            {
                var account = await accounts.FindByIdForUserAsync(input.AccountId.Value, input.UserId, token);
                if (account is null) return Result<RecurringTransaction>.Failure([AccountErrors.NotFound]);
                if (account.ArchivedAt is not null) return Result<RecurringTransaction>.Failure([TransactionErrors.AccountArchived]);
                var kind = TransactionKind.FromValue(input.Kind);
                if (kind.RequiresInvestmentAccount && account.Type.Value != "investment")
                    return Result<RecurringTransaction>.Failure([TransactionErrors.KindRequiresInvestmentAccount(input.Kind)]);
            }
            else if (input.CardId is not null)
            {
                var card = await cards.FindByIdForUserAsync(input.CardId.Value, input.UserId, token);
                if (card is null) return Result<RecurringTransaction>.Failure([CardErrors.NotFound]);
                if (card.ArchivedAt is not null) return Result<RecurringTransaction>.Failure([CardErrors.Archived]);
            }

            var repo = ctx.AcquireRepository<IRecurringTransactionRepository>();
            var recurring = RecurringTransaction.Create(
                input.UserId,
                input.Name,
                input.AccountId,
                input.CardId,
                input.Kind,
                input.Amount,
                input.AmountIsEstimate,
                input.Description,
                input.Payee,
                input.SystemCategoryId,
                input.UserCategoryId,
                input.Frequency,
                input.Interval,
                input.DayOfMonth,
                input.Weekday,
                input.StartDate,
                input.EndDate,
                input.MaxOccurrences,
                input.AutoPost,
                timeProvider);

            await repo.AddAsync(recurring, token);
            await ctx.RecordAsync(input.UserId, input.UserId, "recurring-transaction", recurring.Id,
                "recurring.created", now, new
                {
                    recurring.Name,
                    recurring.Frequency,
                    recurring.Interval,
                    recurring.StartDate,
                    recurring.EndDate,
                    recurring.AutoPost
                }, ct: token);

            return Result<RecurringTransaction>.Success(recurring);
        }, cancellationToken: ct);

        return result.IsFailure ? Fail([.. result.Errors]) : Ok(RecurringTransactionDto.From(result.Value!));
    }
}
