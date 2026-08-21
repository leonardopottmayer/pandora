using Pottmayer.Pandora.Modules.Agenda.Abstractions;
using Pottmayer.Pandora.Modules.Agenda.Application.Dtos;
using Pottmayer.Pandora.Modules.Agenda.Application.Errors;
using Pottmayer.Pandora.Modules.Agenda.Application.Mapping;
using Pottmayer.Pandora.Modules.Agenda.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Agenda.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Agenda.Domain.ValueObjects;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Agenda.Application.Commands.CreateEvent;

public sealed class CreateEventCommandHandler(IUnitOfWorkFactory factory, TimeProvider timeProvider)
    : CommandHandlerBase<CreateEventCommand, EventDto>
{
    protected override async Task<Result<EventDto>> HandleAsync(CreateEventCommand request, CancellationToken ct)
    {
        var input = request.Input;

        if (string.IsNullOrWhiteSpace(input.Title))
            return Fail(EventErrors.TitleRequired);

        var status = ParseStatus(input.Status);

        var result = await factory.ExecuteAsync(AgendaModule.Name, async (context, token) =>
        {
            var calendars = context.AcquireRepository<ICalendarRepository>();
            var events = context.AcquireRepository<IEventRepository>();

            var calendar = await calendars.FindAsync(input.UserId, input.CalendarId, token);
            if (calendar is null)
                return Result<EventDto>.Failure([EventErrors.CalendarNotFound]);

            Event created;
            try
            {
                created = Event.Create(
                    input.UserId, input.CalendarId, input.Title, input.Description, input.Location, input.Url,
                    input.StartsAt, input.EndsAt, input.IsAllDay, input.TimeZone ?? "UTC", input.Rrule, status,
                    timeProvider);
            }
            catch (Exception ex) when (ex is FormatException or ArgumentException)
            {
                // A bad/unsupported RRULE, an unknown zone, or an end before the start.
                return Result<EventDto>.Failure([EventErrors.Invalid(ex.Message)]);
            }

            await events.AddAsync(created, token);
            return Result<EventDto>.Success(created.ToDto());
        }, cancellationToken: ct);

        return result;
    }

    private static EventStatus ParseStatus(string? status) =>
        Enum.TryParse<EventStatus>(status, ignoreCase: true, out var s) ? s : EventStatus.Confirmed;
}
