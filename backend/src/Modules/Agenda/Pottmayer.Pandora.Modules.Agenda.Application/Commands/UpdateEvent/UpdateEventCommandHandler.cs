using Pottmayer.Pandora.Modules.Agenda.Abstractions;
using Pottmayer.Pandora.Modules.Agenda.Application.Dtos;
using Pottmayer.Pandora.Modules.Agenda.Application.Errors;
using Pottmayer.Pandora.Modules.Agenda.Application.Mapping;
using Pottmayer.Pandora.Modules.Agenda.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Agenda.Domain.Ports.Repositories;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Agenda.Application.Commands.UpdateEvent;

/// <summary>
/// Applies an edit at one of the three scopes. <c>This</c> upserts an override, <c>ThisAndFuture</c>
/// splits the series (the original gets an <c>UNTIL</c> before the cut and a new event carries the
/// tail), and <c>All</c> mutates the row. Returns the row that now carries the ongoing series — for a
/// split, that is the new tail event.
/// </summary>
public sealed class UpdateEventCommandHandler(IUnitOfWorkFactory factory, TimeProvider timeProvider)
    : CommandHandlerBase<UpdateEventCommand, EventDto>
{
    protected override async Task<Result<EventDto>> HandleAsync(UpdateEventCommand request, CancellationToken ct)
    {
        var input = request.Input;

        if (input.Title is not null && string.IsNullOrWhiteSpace(input.Title))
            return Fail(EventErrors.TitleRequired);

        var result = await factory.ExecuteAsync(AgendaModule.DatabaseKey, async (context, token) =>
        {
            var events = context.AcquireRepository<IEventRepository>();
            var overrides = context.AcquireRepository<IEventOccurrenceOverrideRepository>();

            var ev = await events.FindAsync(input.UserId, input.EventId, token);
            if (ev is null)
                return Result<EventDto>.Failure([EventErrors.NotFound]);

            try
            {
                return input.Scope switch
                {
                    EventEditScope.This => await EditOccurrenceAsync(ev, input, overrides, token),
                    EventEditScope.ThisAndFuture => await SplitAsync(ev, input, events, token),
                    _ => await EditSeriesAsync(ev, input, events, token),
                };
            }
            catch (ArgumentException ex)
            {
                return Result<EventDto>.Failure([EventErrors.Invalid(ex.Message)]);
            }
        }, cancellationToken: ct);

        return result;
    }

    // ── this: an override on a single occurrence ──
    private async Task<Result<EventDto>> EditOccurrenceAsync(
        Event ev, UpdateEventInput input, IEventOccurrenceOverrideRepository overrides, CancellationToken token)
    {
        if (input.OccurrenceStart is not { } occurrence)
            return Result<EventDto>.Failure([EventErrors.OccurrenceRequired]);

        var normalized = occurrence.ToUniversalTime();
        var existing = await overrides.FindAsync(ev.Id, normalized, token);
        if (existing is null)
        {
            var created = EventOccurrenceOverride.Create(ev.Id, ev.UserId, normalized, timeProvider);
            created.Edit(input.StartsAt, input.EndsAt, input.Title, input.Description, input.Location);
            await overrides.AddAsync(created, token);
        }
        else
        {
            existing.Edit(input.StartsAt, input.EndsAt, input.Title, input.Description, input.Location);
            await overrides.UpdateAsync(existing, token);
        }

        return Result<EventDto>.Success(ev.ToDto());
    }

    // ── this-and-future: end the original before the cut, spawn a new event for the tail ──
    private async Task<Result<EventDto>> SplitAsync(
        Event ev, UpdateEventInput input, IEventRepository events, CancellationToken token)
    {
        if (!ev.IsRecurring)
            return Result<EventDto>.Failure([EventErrors.NotRecurring]);
        if (input.OccurrenceStart is not { } occurrence)
            return Result<EventDto>.Failure([EventErrors.OccurrenceRequired]);

        var cut = occurrence.ToUniversalTime();

        // Capture the full rule before truncating the original, so the tail keeps recurring the same way.
        var tailRrule = ev.Rrule;

        var newStart = (input.StartsAt ?? cut).ToUniversalTime();
        var newEnd = (input.EndsAt ?? newStart + ev.Duration).ToUniversalTime();

        ev.EndSeriesBefore(cut);
        await events.UpdateAsync(ev, token);

        var tail = Event.Create(
            ev.UserId,
            input.CalendarId ?? ev.CalendarId,
            input.Title ?? ev.Title,
            input.Description ?? ev.Description,
            input.Location ?? ev.Location,
            input.Url ?? ev.Url,
            newStart,
            newEnd,
            input.IsAllDay ?? ev.IsAllDay,
            ev.TimeZone,
            tailRrule,
            ev.Status,
            timeProvider);

        await events.AddAsync(tail, token);
        return Result<EventDto>.Success(tail.ToDto());
    }

    // ── all: edit the whole series row ──
    private async Task<Result<EventDto>> EditSeriesAsync(
        Event ev, UpdateEventInput input, IEventRepository events, CancellationToken token)
    {
        ev.Update(
            input.Title ?? ev.Title,
            input.Description ?? ev.Description,
            input.Location ?? ev.Location,
            input.Url ?? ev.Url,
            input.StartsAt ?? ev.StartsAt,
            input.EndsAt ?? ev.EndsAt,
            input.IsAllDay ?? ev.IsAllDay,
            input.CalendarId ?? ev.CalendarId);

        await events.UpdateAsync(ev, token);
        return Result<EventDto>.Success(ev.ToDto());
    }
}
