using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Queries;

namespace Pottmayer.Pandora.Modules.Finances.Application.Queries.GetAccount;

public sealed record GetAccountInput(Guid UserId, Guid AccountId);

public sealed class GetAccountQuery(GetAccountInput input)
    : QueryBase<GetAccountInput, AccountDto>(input);
