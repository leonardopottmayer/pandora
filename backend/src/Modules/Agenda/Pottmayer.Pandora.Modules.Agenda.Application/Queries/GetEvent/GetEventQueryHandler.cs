using Pottmayer.Pandora.Modules.Agenda.Abstractions;
using Pottmayer.Pandora.Modules.Agenda.Application.Dtos;
using Pottmayer.Pandora.Modules.Agenda.Application.Errors;
using Pottmayer.Pandora.Modules.Agenda.Application.Mapping;
using Pottmayer.Pandora.Modules.Agenda.Domain.Ports.Repositories;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Agenda.Application.Queries.GetEvent;

/// <summary>The event <em>row</em> (series) — carries the rrule/recurrence the occurrence reads omit.</summary>
public sealed class GetEventQueryHandler(IUnitOfWorkFactory factory)
    : QueryHandlerBase<GetEventQuery, EventDto>
{
    protected override async Task<Result<EventDto>> HandleAsync(
        GetEventQuery request, CancellationToken cancellationToken)
    {
        var result = await factory.ExecuteAsync(AgendaModule.DatabaseKey, async (context, ct) =>
        {
            var events = context.AcquireRepository<IEventRepository>();
            var ev = await events.FindAsync(request.Input.UserId, request.Input.EventId, ct);
            return ev is null
                ? Result<EventDto>.Failure([EventErrors.NotFound])
                : Result<EventDto>.Success(ev.ToDto());
        }, cancellationToken: cancellationToken);

        return result;
    }
}
