using Pottmayer.Pandora.Modules.Agenda.Abstractions;
using Pottmayer.Pandora.Modules.Agenda.Application.Dtos;
using Pottmayer.Pandora.Modules.Agenda.Application.Errors;
using Pottmayer.Pandora.Modules.Agenda.Application.Mapping;
using Pottmayer.Pandora.Modules.Agenda.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Agenda.Domain.ValueObjects;
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
            var alerts = context.AcquireRepository<IAlertRepository>();

            var task = await tasks.FindAsync(input.UserId, input.TaskId, token);
            if (task is null)
                return Result<TaskDto>.Failure([TaskErrors.NotFound]);

            // Only an open recurring task spawns a successor, so a double-tap of "Done" spawns nothing.
            var nextDueAt = task.Status == TaskItemStatus.Done ? null : task.ComputeNextDueAt();

            task.Complete(timeProvider);
            await tasks.UpdateAsync(task, token);

            if (nextDueAt is { } next)
            {
                var spawned = task.SpawnNext(next, timeProvider);
                await tasks.AddAsync(spawned, token);

                foreach (var alert in await alerts.GetBySubjectAsync(input.UserId, AlertSubjectType.Task, task.Id, token))
                    await alerts.AddAsync(alert.CopyFor(spawned.Id, timeProvider), token);
            }

            return Result<TaskDto>.Success(task.ToDto());
        }, cancellationToken: ct);

        return result;
    }
}
