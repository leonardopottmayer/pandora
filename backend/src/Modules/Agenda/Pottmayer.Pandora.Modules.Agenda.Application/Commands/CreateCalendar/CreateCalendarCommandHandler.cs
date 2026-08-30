using Pottmayer.Pandora.Modules.Agenda.Abstractions;
using Pottmayer.Pandora.Modules.Agenda.Application.Dtos;
using Pottmayer.Pandora.Modules.Agenda.Application.Errors;
using Pottmayer.Pandora.Modules.Agenda.Application.Mapping;
using Pottmayer.Pandora.Modules.Agenda.Application.Preferences;
using Pottmayer.Pandora.Modules.Agenda.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Agenda.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Agenda.Domain.ValueObjects;
using Pottmayer.Pandora.Modules.Identity.Abstractions.Ports;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Agenda.Application.Commands.CreateCalendar;

public sealed class CreateCalendarCommandHandler(
    IUnitOfWorkFactory factory, IUserPreferencesReader preferences, TimeProvider timeProvider)
    : CommandHandlerBase<CreateCalendarCommand, CalendarDto>
{
    protected override async Task<Result<CalendarDto>> HandleAsync(CreateCalendarCommand request, CancellationToken ct)
    {
        var input = request.Input;

        if (string.IsNullOrWhiteSpace(input.Name))
            return Fail(CalendarErrors.NameRequired);

        var timeZone = await TimeZoneResolver.ResolveAsync(preferences, input.UserId, input.TimeZone, ct);

        Calendar created;
        try
        {
            created = Calendar.Create(
                input.UserId, input.Name, input.Color, input.IsDefault, timeZone,
                CalendarOrigin.Local, timeProvider);
        }
        catch (ArgumentException ex)
        {
            return Fail(CalendarErrors.InvalidTimeZone(ex.Message));
        }

        await factory.ExecuteAsync(AgendaModule.DatabaseKey, async (context, token) =>
        {
            await context.AcquireRepository<ICalendarRepository>().AddAsync(created, token);
            return true;
        }, cancellationToken: ct);

        return Ok(created.ToDto());
    }
}
