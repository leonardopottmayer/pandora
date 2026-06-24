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

namespace Pottmayer.Pandora.Modules.Finances.Application.Commands.CreateAccount;

public sealed class CreateAccountCommandHandler(IUnitOfWorkFactory factory, TimeProvider timeProvider)
    : CommandHandlerBase<CreateAccountCommand, AccountDto>
{
    protected override async Task<Result<AccountDto>> HandleAsync(CreateAccountCommand request, CancellationToken ct)
    {
        var input = request.Input;
        var now = timeProvider.GetUtcNow();

        if (string.IsNullOrWhiteSpace(input.Name))
            return Fail(AccountErrors.InvalidName);

        if (!AccountType.IsSupported(input.Type))
            return Fail(AccountErrors.InvalidType(input.Type));

        if (!CurrencyCode.TryCreate(input.Currency, out var currency))
            return Fail(AccountErrors.InvalidCurrency(input.Currency));

        var type = AccountType.FromValue(input.Type);

        var result = await factory.ExecuteAsync(FinancesModule.Name, async (ctx, token) =>
        {
            var repo = ctx.AcquireRepository<IAccountRepository>();

            // Name uniqueness is per user, so the check happens here rather than in the aggregate.
            if (await repo.ExistsWithNameAsync(input.UserId, input.Name, null, token))
                return Result<Account>.Failure([AccountErrors.NameAlreadyExists]);

            var account = Account.Create(
                input.UserId, input.Name, type, currency!, input.Institution, input.Description,
                input.Color, input.Icon, input.DisplayOrder, timeProvider);
            await repo.AddAsync(account, token);

            await ctx.RecordAsync(
                input.UserId, input.UserId, AccountEvents.EntityType, account.Id, AccountEvents.Created, now,
                new
                {
                    name = account.Name,
                    type = account.Type.Value,
                    currency = account.Currency.Value,
                    displayOrder = account.DisplayOrder
                },
                ct: token);

            // A positive opening balance becomes a posted opening-balance transaction (ledger is the
            // truth, never a stored balance field — D1). Non-positive values are ignored here.
            if (input.OpeningBalance is > 0m)
            {
                var opening = Transaction.CreateAccountTransaction(
                    input.UserId, account.Id, TransactionKind.OpeningBalance, currency!,
                    input.OpeningBalance.Value, DateOnly.FromDateTime(now.UtcDateTime),
                    description: "", null, null, null, null, post: true, timeProvider,
                    systemDescription: SystemDescription.OpeningBalance());

                await ctx.AcquireRepository<ITransactionRepository>().AddAsync(opening, token);

                await ctx.RecordAsync(
                    input.UserId, input.UserId, TransactionEvents.EntityType, opening.Id, TransactionEvents.Created, now,
                    new
                    {
                        accountId = opening.AccountId,
                        kind = opening.Kind.Value,
                        status = opening.Status.Value,
                        amount = opening.Amount,
                        currency = opening.Currency.Value
                    },
                    ct: token);
            }

            return Result<Account>.Success(account);
        }, cancellationToken: ct);

        return result.IsFailure
            ? Fail([.. result.Errors])
            : Ok(AccountDto.From(result.Value!));
    }
}
