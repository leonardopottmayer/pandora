using Pottmayer.Pandora.Modules.Integrations.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Queries;

namespace Pottmayer.Pandora.Modules.Integrations.Application.Queries.GetEvents;

public sealed record GetIntegrationEventsInput(Guid UserId, int Limit);

public sealed class GetIntegrationEventsQuery(GetIntegrationEventsInput input)
    : QueryBase<GetIntegrationEventsInput, IReadOnlyList<IntegrationEventDto>>(input);
