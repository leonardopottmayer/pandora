using Pottmayer.Pandora.Modules.Agenda.Abstractions;
using Pottmayer.Pandora.Modules.Agenda.Application.Dtos;
using Pottmayer.Pandora.Modules.Agenda.Application.Errors;
using Pottmayer.Pandora.Modules.Agenda.Application.Mapping;
using Pottmayer.Pandora.Modules.Agenda.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Agenda.Domain.Ports.Repositories;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Agenda.Application.Commands.UpdateCalendar;

public sealed class UpdateCalendarCommandHandler(IUnitOfWorkFactory factory, TimeProvider timeProvider)
    : CommandHandlerBase<UpdateCalendarCommand, CalendarDto>
{
    protected override async Task<Result<CalendarDto>> HandleAsync(UpdateCalendarCommand request, CancellationToken ct)
    {
        var input = request.Input;

        if (input.Name is not null && string.IsNullOrWhiteSpace(input.Name))
            return Fail(CalendarErrors.NameRequired);

        // Promoting a calendar to default: demote the current default first, in its own transaction,
        // so the partial unique index (one default per user) never sees two at once. Guarded by the
        // target existing, so a bad id does not leave the user with no default.
        if (input.IsDefault == true)
            await factory.ExecuteAsync(AgendaModule.DatabaseKey, async (context, token) =>
            {
                var calendars = context.AcquireRepository<ICalendarRepository>();
                if (await calendars.FindAsync(input.UserId, input.CalendarId, token) is null)
                    return false;

                var all = await calendars.GetByUserAsync(input.UserId, token);
                foreach (var other in all.Where(c => c.IsDefault && c.Id != input.CalendarId))
                {
                    other.SetDefault(false);
                    await calendars.UpdateAsync(other, token);
                }
                return true;
            }, cancellationToken: ct);

        var result = await factory.ExecuteAsync(AgendaModule.DatabaseKey, async (context, token) =>
        {
            var calendars = context.AcquireRepository<ICalendarRepository>();
            var found = await calendars.FindAsync(input.UserId, input.CalendarId, token);
            if (found is null)
                return Result<CalendarDto>.Failure([CalendarErrors.NotFound]);

            try
            {
                if (input.Name is not null || input.Color is not null || input.IsVisible is not null || input.TimeZone is not null)
                    found.Update(
                        input.Name ?? found.Name,
                        input.Color ?? found.Color,
                        input.IsVisible ?? found.IsVisible,
                        input.TimeZone ?? found.TimeZone);
            }
            catch (ArgumentException ex)
            {
                return Result<CalendarDto>.Failure([CalendarErrors.InvalidTimeZone(ex.Message)]);
            }

            if (input.IsDefault is { } isDefault)
                found.SetDefault(isDefault);
            if (input.Archive)
                found.Archive(timeProvider);

            await calendars.UpdateAsync(found, token);
            return Result<CalendarDto>.Success(found.ToDto());
        }, cancellationToken: ct);

        return result;
    }
}
