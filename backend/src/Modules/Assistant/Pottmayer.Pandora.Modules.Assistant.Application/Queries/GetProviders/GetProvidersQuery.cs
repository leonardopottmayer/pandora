using Pottmayer.Pandora.Modules.Assistant.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Security.Authorization.Abstractions;
using Pottmayer.Pandora.Modules.Assistant.Abstractions;

namespace Pottmayer.Pandora.Modules.Assistant.Application.Queries.GetProviders;

public sealed record GetProvidersInput(Guid UserId);

[RequiresPermission(AssistantPermissions.Profile.Read)]
public sealed class GetProvidersQuery(GetProvidersInput input)
    : QueryBase<GetProvidersInput, IReadOnlyList<AssistantProviderDto>>(input);
