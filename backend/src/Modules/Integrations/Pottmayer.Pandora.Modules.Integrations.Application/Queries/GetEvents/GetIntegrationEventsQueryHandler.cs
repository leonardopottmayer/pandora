using Pottmayer.Pandora.Modules.Integrations.Abstractions;
using Pottmayer.Pandora.Modules.Integrations.Application.Dtos;
using Pottmayer.Pandora.Modules.Integrations.Domain.Ports.Repositories;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Integrations.Application.Queries.GetEvents;

public sealed class GetIntegrationEventsQueryHandler(IUnitOfWorkFactory factory)
    : QueryHandlerBase<GetIntegrationEventsQuery, IReadOnlyList<IntegrationEventDto>>
{
    private const int MaxLimit = 100;

    protected override async Task<Result<IReadOnlyList<IntegrationEventDto>>> HandleAsync(
        GetIntegrationEventsQuery request, CancellationToken cancellationToken)
    {
        var limit = Math.Clamp(request.Input.Limit, 1, MaxLimit);

        var events = await factory.ExecuteAsync(IntegrationsModule.DatabaseKey, async (context, ct) =>
        {
            var repo = context.AcquireRepository<IIntegrationEventLogRepository>();
            return await repo.GetRecentByUserAsync(request.Input.UserId, limit, ct);
        }, cancellationToken: cancellationToken);

        IReadOnlyList<IntegrationEventDto> dtos = [.. events.Select(e => new IntegrationEventDto(
            e.Id,
            e.ExternalAccountId,
            e.Provider,
            e.EventType.Value,
            e.Detail,
            e.OccurredAt))];

        return Ok(dtos);
    }
}
