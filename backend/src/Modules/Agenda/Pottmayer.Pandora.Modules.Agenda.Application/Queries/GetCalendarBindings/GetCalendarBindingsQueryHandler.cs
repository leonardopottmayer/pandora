using Pottmayer.Pandora.Modules.Agenda.Abstractions;
using Pottmayer.Pandora.Modules.Agenda.Application.Dtos;
using Pottmayer.Pandora.Modules.Agenda.Application.Mapping;
using Pottmayer.Pandora.Modules.Agenda.Domain.Ports.Repositories;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Agenda.Application.Queries.GetCalendarBindings;

public sealed class GetCalendarBindingsQueryHandler(IUnitOfWorkFactory factory)
    : QueryHandlerBase<GetCalendarBindingsQuery, IReadOnlyList<CalendarBindingDto>>
{
    protected override async Task<Result<IReadOnlyList<CalendarBindingDto>>> HandleAsync(
        GetCalendarBindingsQuery request, CancellationToken cancellationToken)
    {
        var userId = request.Input.UserId;
        var dtos = await factory.ExecuteAsync(AgendaModule.DatabaseKey, async (context, ct) =>
        {
            var bindings = await context.AcquireRepository<ICalendarBindingRepository>().GetByUserAsync(userId, ct);
            var calendars = (await context.AcquireRepository<ICalendarRepository>().GetByUserAsync(userId, ct))
                .ToDictionary(c => c.Id);

            IReadOnlyList<CalendarBindingDto> list =
                [.. bindings.Where(b => calendars.ContainsKey(b.CalendarId)).Select(b => b.ToDto(calendars[b.CalendarId]))];
            return list;
        }, cancellationToken: cancellationToken);

        return Ok(dtos);
    }
}
