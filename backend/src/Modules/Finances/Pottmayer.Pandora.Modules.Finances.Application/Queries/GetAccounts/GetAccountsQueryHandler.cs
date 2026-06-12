using Pottmayer.Pandora.Modules.Finances.Abstractions;
using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Pandora.Modules.Finances.Domain.Ports.Repositories;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Finances.Application.Queries.GetAccounts;

public sealed class GetAccountsQueryHandler(IUnitOfWorkFactory factory)
    : QueryHandlerBase<GetAccountsQuery, IReadOnlyList<AccountDto>>
{
    protected override async Task<Result<IReadOnlyList<AccountDto>>> HandleAsync(
        GetAccountsQuery request, CancellationToken ct)
    {
        var input = request.Input;

        var accounts = await factory.ExecuteAsync(FinancesModule.Name, async (ctx, token) =>
        {
            var repo = ctx.AcquireRepository<IAccountRepository>();
            return await repo.GetAllForUserAsync(input.UserId, input.IncludeArchived, token);
        }, cancellationToken: ct);

        IReadOnlyList<AccountDto> dtos = [.. accounts.Select(AccountDto.From)];
        return Ok(dtos);
    }
}
