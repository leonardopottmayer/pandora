using Pottmayer.Pandora.Modules.Agenda.Abstractions;
using Pottmayer.Pandora.Modules.Agenda.Application.Dtos;
using Pottmayer.Pandora.Modules.Agenda.Application.Errors;
using Pottmayer.Pandora.Modules.Agenda.Application.Google;
using Pottmayer.Pandora.Modules.Agenda.Application.Mapping;
using Pottmayer.Pandora.Modules.Agenda.Domain.Ports.Repositories;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Agenda.Application.Commands.CompleteTask;

/// <summary>
/// Closes the current instance and, for a recurring task that is still open, inserts the next instance
/// from the RRULE and copies its alerts onto it — two rows, so history survives (doc §5.4).
/// </summary>
public sealed class CompleteTaskCommandHandler(IUnitOfWorkFactory factory, TimeProvider timeProvider)
    : CommandHandlerBase<CompleteTaskCommand, TaskDto>
{
    protected override async Task<Result<TaskDto>> HandleAsync(CompleteTaskCommand request, CancellationToken ct)
    {
        var input = request.Input;

        var result = await factory.ExecuteAsync(AgendaModule.DatabaseKey, async (context, token) =>
        {
            var tasks = context.AcquireRepository<ITaskRepository>();

            var task = await tasks.FindAsync(input.UserId, input.TaskId, token);
            if (task is null)
                return Result<TaskDto>.Failure([TaskErrors.NotFound]);

            var spawned = await TaskCompletion.CompleteAsync(context, task, timeProvider, token);
            await tasks.UpdateAsync(task, token);

            await ExternalTaskLists.MarkChangedAsync(context, task, timeProvider, token);
            if (spawned is not null)
                await ExternalTaskLists.MarkChangedAsync(context, spawned, timeProvider, token);

            return Result<TaskDto>.Success(task.ToDto());
        }, cancellationToken: ct);

        return result;
    }
}
