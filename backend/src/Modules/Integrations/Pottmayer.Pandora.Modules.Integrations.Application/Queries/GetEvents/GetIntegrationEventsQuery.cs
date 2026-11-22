using Pottmayer.Pandora.Modules.Integrations.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Security.Authorization.Abstractions;
using Pottmayer.Pandora.Modules.Integrations.Abstractions;

namespace Pottmayer.Pandora.Modules.Integrations.Application.Queries.GetEvents;

public sealed record GetIntegrationEventsInput(Guid UserId, int Limit);

[RequiresPermission(IntegrationsPermissions.Accounts.Read)]
public sealed class GetIntegrationEventsQuery(GetIntegrationEventsInput input)
    : QueryBase<GetIntegrationEventsInput, IReadOnlyList<IntegrationEventDto>>(input);
