using Pottmayer.Pandora.Modules.Agenda.Abstractions;
using Pottmayer.Pandora.Modules.Agenda.Application.Errors;
using Pottmayer.Pandora.Modules.Agenda.Domain.Ports.Repositories;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Agenda.Application.Commands.DeleteTask;

public sealed class DeleteTaskCommandHandler(IUnitOfWorkFactory factory, TimeProvider timeProvider)
    : CommandHandlerBase<DeleteTaskCommand, bool>
{
    protected override async Task<Result<bool>> HandleAsync(DeleteTaskCommand request, CancellationToken ct)
    {
        var input = request.Input;

        var found = await factory.ExecuteAsync(AgendaModule.DatabaseKey, async (context, token) =>
        {
            var tasks = context.AcquireRepository<ITaskRepository>();
            var task = await tasks.FindAsync(input.UserId, input.TaskId, token);
            if (task is null)
                return false;

            task.Delete(timeProvider);
            await tasks.UpdateAsync(task, token);
            return true;
        }, cancellationToken: ct);

        return found ? Ok(true) : Fail(TaskErrors.NotFound);
    }
}
