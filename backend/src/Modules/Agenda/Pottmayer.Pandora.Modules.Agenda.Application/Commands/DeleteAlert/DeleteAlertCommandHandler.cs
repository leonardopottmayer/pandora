using Pottmayer.Pandora.Modules.Agenda.Abstractions;
using Pottmayer.Pandora.Modules.Agenda.Application.Errors;
using Pottmayer.Pandora.Modules.Agenda.Domain.Ports.Repositories;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Agenda.Application.Commands.DeleteAlert;

public sealed class DeleteAlertCommandHandler(IUnitOfWorkFactory factory)
    : CommandHandlerBase<DeleteAlertCommand, bool>
{
    protected override async Task<Result<bool>> HandleAsync(DeleteAlertCommand request, CancellationToken ct)
    {
        var input = request.Input;

        var found = await factory.ExecuteAsync(AgendaModule.Name, async (context, token) =>
        {
            var alerts = context.AcquireRepository<IAlertRepository>();
            var alert = await alerts.FindAsync(input.UserId, input.AlertId, token);
            if (alert is null)
                return false;

            await alerts.RemoveAsync(alert, token);
            return true;
        }, cancellationToken: ct);

        return found ? Ok(true) : Fail(AlertErrors.NotFound);
    }
}
