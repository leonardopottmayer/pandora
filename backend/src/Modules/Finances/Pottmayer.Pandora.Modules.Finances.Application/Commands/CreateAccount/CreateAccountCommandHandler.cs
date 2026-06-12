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

            if (await repo.ExistsWithNameAsync(input.UserId, input.Name, null, token))
                return Result<Account>.Failure([AccountErrors.NameAlreadyExists]);

            var account = Account.Create(
                input.UserId, input.Name, type, currency!, input.Institution, input.Description,
                input.Color, input.Icon, input.DisplayOrder, timeProvider);
            await repo.AddAsync(account, token);

            await ctx.RecordAsync(
                input.UserId, input.UserId, "account", account.Id, "account.created", now,
                new
                {
                    name = account.Name,
                    type = account.Type.Value,
                    currency = account.Currency.Value,
                    displayOrder = account.DisplayOrder
                },
                ct: token);

            return Result<Account>.Success(account);
        }, cancellationToken: ct);

        return result.IsFailure
            ? Fail([.. result.Errors])
            : Ok(AccountDto.From(result.Value!));
    }
}
