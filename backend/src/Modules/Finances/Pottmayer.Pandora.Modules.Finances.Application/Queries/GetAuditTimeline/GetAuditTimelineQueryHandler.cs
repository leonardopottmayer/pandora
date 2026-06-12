using Pottmayer.Pandora.Modules.Finances.Abstractions;
using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Pandora.Modules.Finances.Domain.Errors;
using Pottmayer.Pandora.Modules.Finances.Domain.Ports.Repositories;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Finances.Application.Queries.GetAuditTimeline;

public sealed class GetAuditTimelineQueryHandler(IUnitOfWorkFactory factory)
    : QueryHandlerBase<GetAuditTimelineQuery, IReadOnlyList<AuditEventDto>>
{
    private const int MaxTake = 200;

    protected override async Task<Result<IReadOnlyList<AuditEventDto>>> HandleAsync(
        GetAuditTimelineQuery request, CancellationToken ct)
    {
        var input = request.Input;
        var take = input.Take is <= 0 or > MaxTake ? 50 : input.Take;
        var skip = input.Skip < 0 ? 0 : input.Skip;

        var byEntity = !string.IsNullOrWhiteSpace(input.EntityType) && input.EntityId is not null;
        if (!byEntity && input.CorrelationId is null)
            return Fail(AuditErrors.MissingFilter);

        var events = await factory.ExecuteAsync(FinancesModule.Name, async (ctx, token) =>
        {
            var audit = ctx.AcquireRepository<IAuditEventRepository>();
            return byEntity
                ? await audit.GetByEntityAsync(input.UserId, input.EntityType!, input.EntityId!.Value, skip, take, token)
                : await audit.GetByCorrelationAsync(input.UserId, input.CorrelationId!.Value, skip, take, token);
        }, cancellationToken: ct);

        IReadOnlyList<AuditEventDto> dtos = [.. events.Select(AuditEventDto.From)];
        return Ok(dtos);
    }
}
