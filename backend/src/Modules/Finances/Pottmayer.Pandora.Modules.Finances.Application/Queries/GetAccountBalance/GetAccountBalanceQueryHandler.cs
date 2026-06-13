using Pottmayer.Pandora.Modules.Finances.Abstractions;
using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Pandora.Modules.Finances.Domain.Errors;
using Pottmayer.Pandora.Modules.Finances.Domain.Ports.Repositories;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Finances.Application.Queries.GetAccountBalance;

public sealed class GetAccountBalanceQueryHandler(IUnitOfWorkFactory factory)
    : QueryHandlerBase<GetAccountBalanceQuery, AccountBalanceDto>
{
    protected override async Task<Result<AccountBalanceDto>> HandleAsync(
        GetAccountBalanceQuery request, CancellationToken ct)
    {
        var input = request.Input;

        var dto = await factory.ExecuteAsync(FinancesModule.Name, async (ctx, token) =>
        {
            var accounts = ctx.AcquireRepository<IAccountRepository>();
            var account = await accounts.FindByIdForUserAsync(input.AccountId, input.UserId, token);
            if (account is null)
                return null;

            var transactions = ctx.AcquireRepository<ITransactionRepository>();
            var posted = await transactions.GetPostedBalanceAsync(account.Id, input.UserId, token);
            var projected = await transactions.GetProjectedBalanceAsync(account.Id, input.UserId, token);

            return new AccountBalanceDto(account.Id, account.Currency.Value, posted, projected);
        }, cancellationToken: ct);

        return dto is null ? Fail(AccountErrors.NotFound) : Ok(dto);
    }
}
