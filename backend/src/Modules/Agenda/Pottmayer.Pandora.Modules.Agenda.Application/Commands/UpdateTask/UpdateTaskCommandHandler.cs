using Pottmayer.Pandora.Modules.Agenda.Abstractions;
using Pottmayer.Pandora.Modules.Agenda.Application.Dtos;
using Pottmayer.Pandora.Modules.Agenda.Application.Errors;
using Pottmayer.Pandora.Modules.Agenda.Application.Mapping;
using Pottmayer.Pandora.Modules.Agenda.Domain.Ports.Repositories;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Agenda.Application.Commands.UpdateTask;

public sealed class UpdateTaskCommandHandler(IUnitOfWorkFactory factory)
    : CommandHandlerBase<UpdateTaskCommand, TaskDto>
{
    protected override async Task<Result<TaskDto>> HandleAsync(UpdateTaskCommand request, CancellationToken ct)
    {
        var input = request.Input;

        if (string.IsNullOrWhiteSpace(input.Title))
            return Fail(TaskErrors.TitleRequired);

        var result = await factory.ExecuteAsync(AgendaModule.DatabaseKey, async (context, token) =>
        {
            var tasks = context.AcquireRepository<ITaskRepository>();
            var task = await tasks.FindAsync(input.UserId, input.TaskId, token);
            if (task is null)
                return Result<TaskDto>.Failure([TaskErrors.NotFound]);

            try
            {
                task.Update(input.Title, input.Notes, input.DueAt, input.DueHasTime, input.Priority);
            }
            catch (InvalidOperationException ex)
            {
                // A recurring task cannot drop its due date.
                return Result<TaskDto>.Failure([TaskErrors.InvalidRecurrence(ex.Message)]);
            }

            await tasks.UpdateAsync(task, token);
            return Result<TaskDto>.Success(task.ToDto());
        }, cancellationToken: ct);

        return result;
    }
}
