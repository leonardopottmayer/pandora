using Pottmayer.Pandora.Modules.Agenda.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Agenda.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Agenda.Domain.ValueObjects;
using Pottmayer.Tars.Data.Abstractions.DataContext;

namespace Pottmayer.Pandora.Modules.Agenda.Application.Google;

/// <summary>What the task handlers do after writing in a list bound to Google Tasks: mark the task for the next push.</summary>
internal static class ExternalTaskLists
{
    /// <summary>
    /// Pandora changed <paramref name="task"/> (created, edited, completed, reopened, deleted): when its list is
    /// bound, the task's link waits for the next push, in the same transaction. A new task gets its link here.
    /// </summary>
    public static async Task MarkChangedAsync(IDataContext context, TaskItem task, TimeProvider timeProvider, CancellationToken ct)
    {
        var binding = await context.AcquireRepository<ITaskListBindingRepository>().FindByListAsync(task.ListId, ct);
        if (binding is null)
            return;

        var links = context.AcquireRepository<ITaskLinkRepository>();
        var link = await links.FindByTaskAsync(task.Id, ct);
        if (link is null)
        {
            if (task.DeletedAt is null)
                await links.AddAsync(TaskLink.CreateLocal(binding.Id, task.Id, timeProvider), ct);
            return;
        }

        link.MarkPending(timeProvider.GetUtcNow());
        await links.UpdateAsync(link, ct);
    }
}

/// <summary>Completing a task, from the user or from Google: a recurring one spawns its next instance, alerts copied.</summary>
internal static class TaskCompletion
{
    /// <summary>Completes <paramref name="task"/> (the caller saves it) and returns the next instance it spawned, already added.</summary>
    public static async Task<TaskItem?> CompleteAsync(IDataContext context, TaskItem task, TimeProvider timeProvider, CancellationToken ct)
    {
        // Only an open recurring task spawns a successor, so a double-tap of "Done" spawns nothing.
        var nextDueAt = task.Status == TaskItemStatus.Done ? null : task.ComputeNextDueAt();
        task.Complete(timeProvider);
        if (nextDueAt is not { } next)
            return null;

        var spawned = task.SpawnNext(next, timeProvider);
        await context.AcquireRepository<ITaskRepository>().AddAsync(spawned, ct);

        var alerts = context.AcquireRepository<IAlertRepository>();
        foreach (var alert in await alerts.GetBySubjectAsync(task.UserId, AlertSubjectType.Task, task.Id, ct))
            await alerts.AddAsync(alert.CopyFor(spawned.Id, timeProvider), ct);
        return spawned;
    }
}
