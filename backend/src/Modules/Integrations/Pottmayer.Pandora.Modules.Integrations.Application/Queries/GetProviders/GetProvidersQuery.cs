using Pottmayer.Pandora.Modules.Integrations.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Security.Authorization.Abstractions;
using Pottmayer.Pandora.Modules.Integrations.Abstractions;

namespace Pottmayer.Pandora.Modules.Integrations.Application.Queries.GetProviders;

public sealed record GetProvidersInput(Guid UserId);

[RequiresPermission(IntegrationsPermissions.Accounts.Read)]
public sealed class GetProvidersQuery(GetProvidersInput input)
    : QueryBase<GetProvidersInput, IReadOnlyList<ProviderCatalogItemDto>>(input);
