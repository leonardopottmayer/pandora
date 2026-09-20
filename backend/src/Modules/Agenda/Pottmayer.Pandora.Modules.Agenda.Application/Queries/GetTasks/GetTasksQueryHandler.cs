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

        IEnumerable<TaskItem> filtered = tasks;
        if (input.Due is { } bucket)
        {
            // Buckets divide on the user's calendar day, not the UTC day, so a task due late tonight
            // is "today" for a UTC−3 user rather than tomorrow.
            var zone = await timeZones.ResolveAsync(input.UserId, ct: cancellationToken);
            var today = DayBoundary.LocalToday(zone, timeProvider.GetUtcNow());
            var todayStart = DayBoundary.StartOfDay(zone, today);
            var tomorrowStart = DayBoundary.StartOfDay(zone, today.AddDays(1));
            var weekEnd = DayBoundary.StartOfDay(zone, today.AddDays(7));
            filtered = tasks.Where(t => MatchesBucket(t, bucket, todayStart, tomorrowStart, weekEnd));
        }

        IReadOnlyList<TaskDto> dtos = [.. filtered.Select(t => t.ToDto())];
        return Ok(dtos);
    }

    private static bool MatchesBucket(
        TaskItem task, TaskDueBucket bucket,
        DateTimeOffset todayStart, DateTimeOffset tomorrowStart, DateTimeOffset weekEnd)
    {
        if (bucket == TaskDueBucket.None)
            return task.DueAt is null;
        if (task.DueAt is not { } due)
            return false;

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
