using Pottmayer.Pandora.Modules.Integrations.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Security.Authorization.Abstractions;
using Pottmayer.Pandora.Modules.Integrations.Abstractions;

namespace Pottmayer.Pandora.Modules.Integrations.Application.Queries.GetAccounts;

public sealed record GetAccountsInput(Guid UserId);

[RequiresPermission(IntegrationsPermissions.Accounts.Read)]
public sealed class GetAccountsQuery(GetAccountsInput input)
    : QueryBase<GetAccountsInput, IReadOnlyList<ExternalAccountDto>>(input);
