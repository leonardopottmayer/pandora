using Pottmayer.Pandora.Modules.Agenda.Abstractions;
using Pottmayer.Pandora.Modules.Agenda.Application.Dtos;
using Pottmayer.Pandora.Modules.Agenda.Application.Errors;
using Pottmayer.Pandora.Modules.Agenda.Application.Mapping;
using Pottmayer.Pandora.Modules.Agenda.Domain.Ports.Repositories;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Agenda.Application.Commands.ReopenTask;

public sealed class ReopenTaskCommandHandler(IUnitOfWorkFactory factory)
    : CommandHandlerBase<ReopenTaskCommand, TaskDto>
{
    protected override async Task<Result<TaskDto>> HandleAsync(ReopenTaskCommand request, CancellationToken ct)
    {
        var input = request.Input;

        var task = await factory.ExecuteAsync(AgendaModule.DatabaseKey, async (context, token) =>
        {
            var tasks = context.AcquireRepository<ITaskRepository>();
            var found = await tasks.FindAsync(input.UserId, input.TaskId, token);
            if (found is null)
                return null;

            found.Reopen();
            await tasks.UpdateAsync(found, token);
            return found;
        }, cancellationToken: ct);

        return task is null ? Fail(TaskErrors.NotFound) : Ok(task.ToDto());
    }
}
