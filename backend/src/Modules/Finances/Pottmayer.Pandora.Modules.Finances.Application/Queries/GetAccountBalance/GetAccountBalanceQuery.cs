using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Security.Authorization.Abstractions;
using Pottmayer.Pandora.Modules.Finances.Abstractions;

namespace Pottmayer.Pandora.Modules.Finances.Application.Queries.GetAccountBalance;

public sealed record GetAccountBalanceInput(Guid UserId, Guid AccountId);

[RequiresPermission(FinancesPermissions.Accounts.Read)]
public sealed class GetAccountBalanceQuery(GetAccountBalanceInput input)
    : QueryBase<GetAccountBalanceInput, AccountBalanceDto>(input);
