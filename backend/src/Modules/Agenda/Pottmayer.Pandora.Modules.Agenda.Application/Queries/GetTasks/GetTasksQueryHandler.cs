using Pottmayer.Pandora.Modules.Agenda.Abstractions;
using Pottmayer.Pandora.Modules.Agenda.Application.Dtos;
using Pottmayer.Pandora.Modules.Agenda.Application.Mapping;
using Pottmayer.Pandora.Modules.Agenda.Application.Preferences;
using Pottmayer.Pandora.Modules.Agenda.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Agenda.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Identity.Abstractions.Ports;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Agenda.Application.Queries.GetTasks;

public sealed class GetTasksQueryHandler(
    IUnitOfWorkFactory factory, IEffectiveTimeZoneResolver timeZones, TimeProvider timeProvider)
    : QueryHandlerBase<GetTasksQuery, IReadOnlyList<TaskDto>>
{
    protected override async Task<Result<IReadOnlyList<TaskDto>>> HandleAsync(
        GetTasksQuery request, CancellationToken cancellationToken)
    {
        var input = request.Input;

        var tasks = await factory.ExecuteAsync(AgendaModule.DatabaseKey, async (context, ct) =>
        {
            var repo = context.AcquireRepository<ITaskRepository>();
            return await repo.GetByUserAsync(input.UserId, input.ListId, input.Status, ct);
        }, cancellationToken: cancellationToken);

        // Buckets divide on the user's calendar day, not the UTC day, so a task due late tonight is
        // "today" for a UTC−3 user rather than tomorrow. A date-only due is compared as a date.
        var zone = await timeZones.ResolveAsync(input.UserId, ct: cancellationToken);
        IEnumerable<TaskItem> filtered = tasks;
        if (input.Due is { } bucket)
        {
            var today = DayBoundary.LocalToday(zone, timeProvider.GetUtcNow());
            filtered = tasks.Where(t => MatchesBucket(t, bucket, zone, today));
        }

        IReadOnlyList<TaskDto> dtos = [.. filtered.Select(t => t.ToDto(zone))];
        return Ok(dtos);
    }

    private static bool MatchesBucket(TaskItem task, TaskDueBucket bucket, TimeZoneInfo zone, DateOnly today)
    {
        if (bucket == TaskDueBucket.None)
            return task.DueAt is null;
        if (task.DueAt is not { } due)
            return false;

        // A timed due falls on the viewer's day; a date-only due is its own date wherever it was set.
        var day = task.DueHasTime ? DayBoundary.LocalToday(zone, due) : task.LocalDueDate()!.Value;
        return bucket switch
        {
            TaskDueBucket.Overdue => day < today,
            TaskDueBucket.Today => day == today,
            TaskDueBucket.Week => day >= today && day < today.AddDays(7),
            TaskDueBucket.Later => day >= today.AddDays(7),
            _ => false,
        };
    }
}
