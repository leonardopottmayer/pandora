using Pottmayer.Pandora.Modules.Agenda.Abstractions;
using Pottmayer.Pandora.Modules.Agenda.Application.Errors;
using Pottmayer.Pandora.Modules.Agenda.Domain.Ports.Repositories;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Agenda.Application.Commands.DeleteCalendar;

public sealed class DeleteCalendarCommandHandler(IUnitOfWorkFactory factory)
    : CommandHandlerBase<DeleteCalendarCommand, bool>
{
    protected override async Task<Result<bool>> HandleAsync(DeleteCalendarCommand request, CancellationToken ct)
    {
        var input = request.Input;

        var outcome = await factory.ExecuteAsync(AgendaModule.Name, async (context, token) =>
        {
            var calendars = context.AcquireRepository<ICalendarRepository>();
            var events = context.AcquireRepository<IEventRepository>();

            var calendar = await calendars.FindAsync(input.UserId, input.CalendarId, token);
            if (calendar is null)
                return Outcome.Missing;

            if (await events.HasLiveEventsAsync(input.UserId, input.CalendarId, token))
                return Outcome.NotEmpty;

            await calendars.RemoveAsync(calendar, token);
            return Outcome.Deleted;
        }, cancellationToken: ct);

        return outcome switch
        {
            Outcome.Missing => Fail(CalendarErrors.NotFound),
            Outcome.NotEmpty => Fail(CalendarErrors.NotEmpty),
            _ => Ok(true),
        };
    }

    private enum Outcome { Missing, NotEmpty, Deleted }
}
