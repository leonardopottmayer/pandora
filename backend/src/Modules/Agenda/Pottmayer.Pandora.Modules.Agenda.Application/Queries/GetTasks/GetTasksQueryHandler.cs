using Pottmayer.Pandora.Modules.Agenda.Abstractions;
using Pottmayer.Pandora.Modules.Agenda.Application.Dtos;
using Pottmayer.Pandora.Modules.Agenda.Application.Mapping;
using Pottmayer.Pandora.Modules.Agenda.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Agenda.Domain.Ports.Repositories;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Agenda.Application.Queries.GetTasks;

public sealed class GetTasksQueryHandler(IUnitOfWorkFactory factory, TimeProvider timeProvider)
    : QueryHandlerBase<GetTasksQuery, IReadOnlyList<TaskDto>>
{
    protected override async Task<Result<IReadOnlyList<TaskDto>>> HandleAsync(
        GetTasksQuery request, CancellationToken cancellationToken)
    {
        var input = request.Input;

        var tasks = await factory.ExecuteAsync(AgendaModule.Name, async (context, ct) =>
        {
            var repo = context.AcquireRepository<ITaskRepository>();
            return await repo.GetByUserAsync(input.UserId, input.ListId, input.Status, ct);
        }, cancellationToken: cancellationToken);

        var filtered = input.Due is { } bucket
            ? tasks.Where(t => MatchesBucket(t, bucket, timeProvider.GetUtcNow()))
            : tasks;

        IReadOnlyList<TaskDto> dtos = [.. filtered.Select(t => t.ToDto())];
        return Ok(dtos);
    }

    private static bool MatchesBucket(TaskItem task, TaskDueBucket bucket, DateTimeOffset now)
    {
        if (bucket == TaskDueBucket.None)
            return task.DueAt is null;
        if (task.DueAt is not { } due)
            return false;

        var todayStart = new DateTimeOffset(now.UtcDateTime.Date, TimeSpan.Zero);
        var tomorrowStart = todayStart.AddDays(1);
        var weekEnd = todayStart.AddDays(7);

        return bucket switch
        {
            TaskDueBucket.Overdue => due < todayStart,
            TaskDueBucket.Today => due >= todayStart && due < tomorrowStart,
            TaskDueBucket.Week => due >= todayStart && due < weekEnd,
            TaskDueBucket.Later => due >= weekEnd,
            _ => false,
        };
    }
}
