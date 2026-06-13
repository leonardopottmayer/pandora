using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Queries;

namespace Pottmayer.Pandora.Modules.Finances.Application.Queries.GetAccounts;

public sealed record GetAccountsInput(Guid UserId, bool IncludeArchived, IReadOnlyList<Guid>? TagIds = null);

public sealed class GetAccountsQuery(GetAccountsInput input)
    : QueryBase<GetAccountsInput, IReadOnlyList<AccountDto>>(input);
