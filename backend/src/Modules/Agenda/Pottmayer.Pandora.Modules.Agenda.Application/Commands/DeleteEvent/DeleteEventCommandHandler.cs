using Pottmayer.Pandora.Modules.Agenda.Abstractions;
using Pottmayer.Pandora.Modules.Agenda.Application.Errors;
using Pottmayer.Pandora.Modules.Agenda.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Agenda.Domain.Ports.Repositories;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Agenda.Application.Commands.DeleteEvent;

public sealed class DeleteEventCommandHandler(IUnitOfWorkFactory factory, TimeProvider timeProvider)
    : CommandHandlerBase<DeleteEventCommand, bool>
{
    protected override async Task<Result<bool>> HandleAsync(DeleteEventCommand request, CancellationToken ct)
    {
        var input = request.Input;

        var result = await factory.ExecuteAsync(AgendaModule.Name, async (context, token) =>
        {
            var events = context.AcquireRepository<IEventRepository>();
            var overrides = context.AcquireRepository<IEventOccurrenceOverrideRepository>();

            var ev = await events.FindAsync(input.UserId, input.EventId, token);
            if (ev is null)
                return Result<bool>.Failure([EventErrors.NotFound]);

            try
            {
                switch (input.Scope)
                {
                    case EventEditScope.This:
                        if (input.OccurrenceStart is not { } occurrence)
                            return Result<bool>.Failure([EventErrors.OccurrenceRequired]);
                        await CancelOccurrenceAsync(ev, occurrence.ToUniversalTime(), overrides, token);
                        break;

                    case EventEditScope.ThisAndFuture:
                        if (!ev.IsRecurring)
                            return Result<bool>.Failure([EventErrors.NotRecurring]);
                        if (input.OccurrenceStart is not { } cut)
                            return Result<bool>.Failure([EventErrors.OccurrenceRequired]);
                        ev.EndSeriesBefore(cut.ToUniversalTime());
                        await events.UpdateAsync(ev, token);
                        break;

                    default:
                        ev.Delete(timeProvider);
                        await events.UpdateAsync(ev, token);
                        break;
                }
            }
            catch (ArgumentException ex)
            {
                return Result<bool>.Failure([EventErrors.Invalid(ex.Message)]);
            }

            return Result<bool>.Success(true);
        }, cancellationToken: ct);

        return result;
    }

    private async Task CancelOccurrenceAsync(
        Event ev, DateTimeOffset occurrence, IEventOccurrenceOverrideRepository overrides, CancellationToken token)
    {
        var existing = await overrides.FindAsync(ev.Id, occurrence, token);
        if (existing is null)
        {
            var created = EventOccurrenceOverride.Create(ev.Id, ev.UserId, occurrence, timeProvider);
            created.Cancel();
            await overrides.AddAsync(created, token);
        }
        else
        {
            existing.Cancel();
            await overrides.UpdateAsync(existing, token);
        }
    }
}
