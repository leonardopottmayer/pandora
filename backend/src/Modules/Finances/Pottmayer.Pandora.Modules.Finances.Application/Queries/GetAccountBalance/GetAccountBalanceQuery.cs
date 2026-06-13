using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Queries;

namespace Pottmayer.Pandora.Modules.Finances.Application.Queries.GetAccountBalance;

public sealed record GetAccountBalanceInput(Guid UserId, Guid AccountId);

public sealed class GetAccountBalanceQuery(GetAccountBalanceInput input)
    : QueryBase<GetAccountBalanceInput, AccountBalanceDto>(input);
