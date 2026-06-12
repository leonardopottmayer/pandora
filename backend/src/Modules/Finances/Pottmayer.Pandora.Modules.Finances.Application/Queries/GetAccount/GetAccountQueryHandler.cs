using Pottmayer.Pandora.Modules.Finances.Abstractions;
using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Pandora.Modules.Finances.Domain.Errors;
using Pottmayer.Pandora.Modules.Finances.Domain.Ports.Repositories;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Finances.Application.Queries.GetAccount;

public sealed class GetAccountQueryHandler(IUnitOfWorkFactory factory)
    : QueryHandlerBase<GetAccountQuery, AccountDto>
{
    protected override async Task<Result<AccountDto>> HandleAsync(GetAccountQuery request, CancellationToken ct)
    {
        var input = request.Input;

        var account = await factory.ExecuteAsync(FinancesModule.Name, async (ctx, token) =>
        {
            var repo = ctx.AcquireRepository<IAccountRepository>();
            return await repo.FindByIdForUserAsync(input.AccountId, input.UserId, token);
        }, cancellationToken: ct);

        return account is null
            ? Fail(AccountErrors.NotFound)
            : Ok(AccountDto.From(account));
    }
}
