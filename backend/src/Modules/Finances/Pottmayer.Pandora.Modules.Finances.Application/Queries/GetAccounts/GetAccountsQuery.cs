using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Security.Authorization.Abstractions;
using Pottmayer.Pandora.Modules.Finances.Abstractions;

namespace Pottmayer.Pandora.Modules.Finances.Application.Queries.GetAccounts;

public sealed record GetAccountsInput(Guid UserId, bool IncludeArchived, IReadOnlyList<Guid>? TagIds = null);

[RequiresPermission(FinancesPermissions.Accounts.Read)]
public sealed class GetAccountsQuery(GetAccountsInput input)
    : QueryBase<GetAccountsInput, IReadOnlyList<AccountDto>>(input);
