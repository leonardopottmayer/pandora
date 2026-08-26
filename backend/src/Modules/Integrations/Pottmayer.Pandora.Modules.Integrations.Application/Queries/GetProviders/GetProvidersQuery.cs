using Pottmayer.Pandora.Modules.Integrations.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Queries;

namespace Pottmayer.Pandora.Modules.Integrations.Application.Queries.GetProviders;

public sealed record GetProvidersInput(Guid UserId);

public sealed class GetProvidersQuery(GetProvidersInput input)
    : QueryBase<GetProvidersInput, IReadOnlyList<ProviderCatalogItemDto>>(input);
