using Pottmayer.Pandora.Modules.Assistant.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Queries;

namespace Pottmayer.Pandora.Modules.Assistant.Application.Queries.GetProviders;

public sealed record GetProvidersInput(Guid UserId);

public sealed class GetProvidersQuery(GetProvidersInput input)
    : QueryBase<GetProvidersInput, IReadOnlyList<AssistantProviderDto>>(input);
