using Pottmayer.Pandora.Modules.Agenda.Abstractions;
using Pottmayer.Pandora.Modules.Agenda.Application.Dtos;
using Pottmayer.Pandora.Modules.Agenda.Application.Mapping;
using Pottmayer.Pandora.Modules.Agenda.Domain.Ports.Repositories;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Agenda.Application.Queries.GetCalendars;

public sealed class GetCalendarsQueryHandler(IUnitOfWorkFactory factory)
    : QueryHandlerBase<GetCalendarsQuery, IReadOnlyList<CalendarDto>>
{
    protected override async Task<Result<IReadOnlyList<CalendarDto>>> HandleAsync(
        GetCalendarsQuery request, CancellationToken cancellationToken)
    {
        var calendars = await factory.ExecuteAsync(AgendaModule.Name, async (context, ct) =>
        {
            var repo = context.AcquireRepository<ICalendarRepository>();
            return await repo.GetByUserAsync(request.Input.UserId, ct);
        }, cancellationToken: cancellationToken);

        IReadOnlyList<CalendarDto> dtos = [.. calendars.Select(c => c.ToDto())];
        return Ok(dtos);
    }
}
