using Pottmayer.Pandora.Modules.Agenda.Abstractions;
using Pottmayer.Pandora.Modules.Agenda.Application.Errors;
using Pottmayer.Pandora.Modules.Agenda.Application.Google;
using Pottmayer.Pandora.Modules.Agenda.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Agenda.Domain.ValueObjects;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Agenda.Application.Commands.DeleteAlert;

public sealed class DeleteAlertCommandHandler(IUnitOfWorkFactory factory, TimeProvider timeProvider)
    : CommandHandlerBase<DeleteAlertCommand, bool>
{
    protected override async Task<Result<bool>> HandleAsync(DeleteAlertCommand request, CancellationToken ct)
    {
        var input = request.Input;

        var found = await factory.ExecuteAsync(AgendaModule.DatabaseKey, async (context, token) =>
        {
            var alerts = context.AcquireRepository<IAlertRepository>();
            var alert = await alerts.FindAsync(input.UserId, input.AlertId, token);
            if (alert is null)
                return false;

            await alerts.RemoveAsync(alert, token);

            // A Google event's alerts are its reminders there.
            if (alert.SubjectType == AlertSubjectType.Event
                && await context.AcquireRepository<IEventRepository>().FindAsync(input.UserId, alert.SubjectId, token) is { } ev)
                await ExternalCalendars.MarkChangedAsync(context, ev, timeProvider, token);
            return true;
        }, cancellationToken: ct);

        return found ? Ok(true) : Fail(AlertErrors.NotFound);
    }
}
