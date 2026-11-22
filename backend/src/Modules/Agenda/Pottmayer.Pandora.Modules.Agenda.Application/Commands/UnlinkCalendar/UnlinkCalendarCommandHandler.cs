using Pottmayer.Pandora.Modules.Agenda.Abstractions;
using Pottmayer.Pandora.Modules.Agenda.Application.Errors;
using Pottmayer.Pandora.Modules.Agenda.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Agenda.Domain.Ports.Repositories;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.DataContext;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Agenda.Application.Commands.UnlinkCalendar;

public sealed class UnlinkCalendarCommandHandler(IUnitOfWorkFactory factory)
    : CommandHandlerBase<UnlinkCalendarCommand, bool>
{
    protected override async Task<Result<bool>> HandleAsync(UnlinkCalendarCommand request, CancellationToken ct)
    {
        var input = request.Input;
        var found = await factory.ExecuteAsync(AgendaModule.DatabaseKey, async (context, token) =>
        {
            var binding = await context.AcquireRepository<ICalendarBindingRepository>().FindAsync(input.UserId, input.BindingId, token);
            if (binding is null)
                return false;

            await CalendarUnlinking.UnlinkAsync(context, binding, token);
            return true;
        }, cancellationToken: ct);

        return found ? Ok(true) : Fail(GoogleCalendarErrors.BindingNotFound);
    }
}

/// <summary>Removes a binding and its links, and turns its calendar back into a local one.</summary>
internal static class CalendarUnlinking
{
    public static async Task UnlinkAsync(IDataContext context, CalendarBinding binding, CancellationToken ct)
    {
        var links = context.AcquireRepository<IEventLinkRepository>();
        foreach (var link in await links.GetByBindingAsync(binding.Id, ct))
            await links.RemoveAsync(link, ct);

        var calendars = context.AcquireRepository<ICalendarRepository>();
        var calendar = await calendars.FindAsync(binding.UserId, binding.CalendarId, ct);
        if (calendar is not null)
        {
            calendar.Detach();
            await calendars.UpdateAsync(calendar, ct);
        }

        await context.AcquireRepository<ICalendarBindingRepository>().RemoveAsync(binding, ct);
    }
}
