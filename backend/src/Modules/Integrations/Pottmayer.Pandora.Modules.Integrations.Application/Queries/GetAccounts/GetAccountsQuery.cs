using Pottmayer.Pandora.Modules.Integrations.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Queries;

namespace Pottmayer.Pandora.Modules.Integrations.Application.Queries.GetAccounts;

public sealed record GetAccountsInput(Guid UserId);

public sealed class GetAccountsQuery(GetAccountsInput input)
    : QueryBase<GetAccountsInput, IReadOnlyList<ExternalAccountDto>>(input);
