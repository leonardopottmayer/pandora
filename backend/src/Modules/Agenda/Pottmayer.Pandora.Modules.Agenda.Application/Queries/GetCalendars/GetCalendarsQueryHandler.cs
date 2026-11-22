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
        var (calendars, writable) = await factory.ExecuteAsync(AgendaModule.DatabaseKey, async (context, ct) =>
        {
            var userId = request.Input.UserId;
            // A Google calendar is read-only here unless its binding sends Pandora's edits there.
            var writable = (await context.AcquireRepository<ICalendarBindingRepository>().GetByUserAsync(userId, ct))
                .Where(b => b.CanWrite).Select(b => b.CalendarId).ToHashSet();
            return (await context.AcquireRepository<ICalendarRepository>().GetByUserAsync(userId, ct), writable);
        }, cancellationToken: cancellationToken);

        IReadOnlyList<CalendarDto> dtos =
            [.. calendars.Select(c => c.ToDto(isReadOnly: c.IsExternal && !writable.Contains(c.Id)))];
        return Ok(dtos);
    }
}
