using Pottmayer.Pandora.Modules.Agenda.Abstractions;
using Pottmayer.Pandora.Modules.Agenda.Application.Dtos;
using Pottmayer.Pandora.Modules.Agenda.Application.Errors;
using Pottmayer.Pandora.Modules.Agenda.Application.Mapping;
using Pottmayer.Pandora.Modules.Agenda.Application.Preferences;
using Pottmayer.Pandora.Modules.Agenda.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Agenda.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Identity.Abstractions.Ports;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Agenda.Application.Commands.CreateTask;

public sealed class CreateTaskCommandHandler(
    IUnitOfWorkFactory factory, IUserPreferencesReader preferences, TimeProvider timeProvider)
    : CommandHandlerBase<CreateTaskCommand, TaskDto>
{
    protected override async Task<Result<TaskDto>> HandleAsync(CreateTaskCommand request, CancellationToken ct)
    {
        var input = request.Input;

        if (string.IsNullOrWhiteSpace(input.Title))
            return Fail(TaskErrors.TitleRequired);

        // Only a top-level task carries a zone (a subtask inherits its parent's); resolve it before
        // the unit of work so the preference read is not nested inside the Agenda transaction.
        var timeZone = input.ParentTaskId is null
            ? await TimeZoneResolver.ResolveAsync(preferences, input.UserId, input.TimeZone, ct)
            : "UTC";

        // Build the task inside the unit of work: a subtask needs its parent, a top-level task its list.
        var result = await factory.ExecuteAsync(AgendaModule.DatabaseKey, async (context, token) =>
        {
            var tasks = context.AcquireRepository<ITaskRepository>();
            var lists = context.AcquireRepository<ITaskListRepository>();

            TaskItem created;
            try
            {
                if (input.ParentTaskId is { } parentId)
                {
                    var parent = await tasks.FindAsync(input.UserId, parentId, token);
                    if (parent is null)
                        return Result<TaskDto>.Failure([TaskErrors.ParentNotFound]);

                    created = TaskItem.CreateSubtask(
                        parent, input.Title, input.Notes, input.DueAt, input.DueHasTime, input.Priority,
                        input.Position, timeProvider);
                }
                else
                {
                    var list = await lists.FindAsync(input.UserId, input.ListId, token);
                    if (list is null)
                        return Result<TaskDto>.Failure([TaskErrors.ListNotFound]);

                    created = TaskItem.Create(
                        input.UserId, input.ListId, input.Title, input.Notes, input.DueAt, input.DueHasTime,
                        input.Priority, timeZone, input.Rrule, input.Position, timeProvider);
                }
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("one level", StringComparison.Ordinal))
            {
                return Result<TaskDto>.Failure([TaskErrors.SubtaskDepthExceeded]);
            }
            catch (Exception ex) when (ex is FormatException or InvalidOperationException or ArgumentException)
            {
                // A bad/unsupported RRULE, a recurring task without a due date, or an unknown zone.
                return Result<TaskDto>.Failure([TaskErrors.InvalidRecurrence(ex.Message)]);
            }

            await tasks.AddAsync(created, token);
            return Result<TaskDto>.Success(created.ToDto());
        }, cancellationToken: ct);

        return result;
    }
}
