using Pottmayer.Pandora.Modules.Agenda.Abstractions;
using Pottmayer.Pandora.Modules.Agenda.Application.Errors;
using Pottmayer.Pandora.Modules.Agenda.Application.Preferences;
using Pottmayer.Pandora.Modules.Agenda.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Agenda.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Agenda.Domain.ValueObjects;
using Pottmayer.Pandora.Modules.Identity.Abstractions.Ports;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.DataContext;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Agenda.Application.Google;

/// <summary>
/// One sync cycle of a task list binding: Pandora's edits first (<see cref="GoogleTasksPush"/>), then
/// Google → Pandora in one unit of work: tasks created, edited, completed, reopened or deleted, and the
/// binding's new cursor.
/// </summary>
/// <remarks>
/// Google Tasks has no sync token. A full listing (first cycle, after a conflict Google won, and every
/// <see cref="GoogleCalendarSync.FullSyncEvery"/>) brings every live task and removes what Google no longer
/// has; the next ones ask for what changed since the last listing started, deletions included.
/// </remarks>
public sealed class GoogleTasksSync(
    IUnitOfWorkFactory factory, IGoogleTasksClient google, CalendarSyncGates gates,
    IEffectiveTimeZoneResolver timeZones, TimeProvider timeProvider)
{
    /// <summary>The cursor starts this much before the listing, so a clock a little off on either side loses nothing.</summary>
    public static readonly TimeSpan ClockSkew = TimeSpan.FromMinutes(1);

    /// <summary>Runs one cycle. Fails only when the binding does not exist; a Google failure is recorded on the binding.</summary>
    public async Task<Result<bool>> SyncAsync(Guid bindingId, CancellationToken ct)
    {
        var gate = gates.For(bindingId);
        if (!await gate.WaitAsync(TimeSpan.Zero, ct))
            return Result<bool>.Success(false);

        try
        {
            var binding = await LoadAsync(bindingId, ct);
            if (binding is null)
                return Result<bool>.Failure([GoogleTasksErrors.BindingNotFound]);

            var pushed = await new GoogleTasksPush(factory, google, timeProvider).PushAsync(binding, ct);
            if (pushed.Fatal is { } fatal)
            {
                await RecordFailureAsync(bindingId, fatal, ct);
                return Result<bool>.Success(false);
            }
            binding = await LoadAsync(bindingId, ct) ?? binding; // a conflict Google won resets the cursor

            var now = timeProvider.GetUtcNow();
            var full = binding.NeedsFullSync(now, GoogleCalendarSync.FullSyncEvery);
            var listing = await google.ListTasksAsync(
                binding.UserId, binding.ExternalAccountId, binding.RemoteListId, full ? null : binding.UpdatedCursor, ct);
            if (!listing.IsSuccess)
            {
                await RecordFailureAsync(bindingId, listing.Errors[0], ct);
                return Result<bool>.Success(false);
            }

            var zone = await TimeZoneResolver.ResolveAsync(timeZones, binding.UserId, null, ct);
            var fetched = new Fetched(listing.Value!, full, now - ClockSkew, now, zone);
            await factory.ExecuteAsync(AgendaModule.DatabaseKey, (context, token) =>
                new Applier(context, binding, fetched, pushed.Failure, timeProvider).ApplyAsync(token), cancellationToken: ct);
            return Result<bool>.Success(true);
        }
        finally
        {
            gate.Release();
        }
    }

    private Task<TaskListBinding?> LoadAsync(Guid bindingId, CancellationToken ct) =>
        factory.ExecuteAsync(AgendaModule.DatabaseKey, (context, token) =>
            context.AcquireRepository<ITaskListBindingRepository>().GetAsync(bindingId, token), cancellationToken: ct);

    private Task RecordFailureAsync(Guid bindingId, Error error, CancellationToken ct) =>
        factory.ExecuteAsync(AgendaModule.DatabaseKey, async (context, token) =>
        {
            var repository = context.AcquireRepository<ITaskListBindingRepository>();
            var binding = await repository.GetAsync(bindingId, token);
            if (binding is null)
                return;

            if (GoogleCalendarErrors.NeedsReconnect(error))
                binding.MarkNeedsReconnect(error.Message);
            else
                binding.RecordFailure(error.Message);
            await repository.UpdateAsync(binding, token);
        }, cancellationToken: ct);

    private sealed record Fetched(IReadOnlyList<GoogleTask> Tasks, bool IsFull, DateTimeOffset Cursor, DateTimeOffset Now, string Zone);

    /// <summary>Applies one fetched cycle inside a unit of work.</summary>
    private sealed class Applier(
        IDataContext context, TaskListBinding binding, Fetched fetched, string? pushFailure, TimeProvider timeProvider)
    {
        private readonly ITaskRepository tasks = context.AcquireRepository<ITaskRepository>();
        private readonly ITaskLinkRepository linkRepository = context.AcquireRepository<ITaskLinkRepository>();
        private readonly Dictionary<string, TaskLink> links = new(StringComparer.Ordinal);
        private readonly Dictionary<Guid, TaskItem> tasksById = [];
        private readonly HashSet<string> seen = new(StringComparer.Ordinal);

        public async Task ApplyAsync(CancellationToken ct)
        {
            var all = await linkRepository.GetByBindingAsync(binding.Id, ct);
            foreach (var link in all.Where(l => l.IsInGoogle))
                links[link.RemoteTaskId!] = link;
            foreach (var task in await tasks.GetByIdsAsync([.. all.Select(l => l.TaskId)], ct))
                if (task.DeletedAt is null)
                    tasksById[task.Id] = task;

            // Top-level tasks first, so a subtask finds its parent.
            foreach (var google in fetched.Tasks.OrderBy(t => t.Parent is not null))
                await ApplyAsync(google, ct);

            if (fetched.IsFull)
            {
                // A pending link may be a task born in Pandora that Google does not have yet.
                foreach (var gone in links.Values.Where(l => !seen.Contains(l.RemoteTaskId!) && !l.IsPending).ToList())
                    await RemoveAsync(gone.RemoteTaskId!, ct);
            }

            var bindings = context.AcquireRepository<ITaskListBindingRepository>();
            var current = await bindings.GetAsync(binding.Id, ct) ?? binding;
            current.RecordSuccess(fetched.Cursor, fetched.IsFull, fetched.Now);
            if (pushFailure is not null)
                current.NotePushFailure(pushFailure);
            await bindings.UpdateAsync(current, ct);
        }

        private async Task ApplyAsync(GoogleTask google, CancellationToken ct)
        {
            seen.Add(google.Id);
            if (google.IsDeleted)
            {
                await RemoveAsync(google.Id, ct);
                return;
            }

            links.TryGetValue(google.Id, out var link);
            TaskItem? task = null;
            if (link is not null)
            {
                // Pandora's edit not sent yet: the next push compares the two and the newer wins.
                if (link.IsPending)
                    return;
                // Pandora's own change coming back.
                if (google.Etag is not null && link.Etag == google.Etag)
                    return;
                tasksById.TryGetValue(link.TaskId, out task);
            }

            try
            {
                if (task is null)
                    await CreateAsync(google, link, ct);
                else
                    await UpdateAsync(task, link!, google, ct);
            }
            catch (Exception ex) when (ex is ArgumentException or FormatException or InvalidOperationException)
            {
                // A task Pandora cannot hold: skip it, keep the cycle.
            }
        }

        private async Task CreateAsync(GoogleTask google, TaskLink? staleLink, CancellationToken ct)
        {
            if (staleLink is not null)
            {
                await linkRepository.RemoveAsync(staleLink, ct);
                links.Remove(google.Id);
            }

            var (dueAt, dueHasTime) = GoogleTaskMapper.Due(google, null, fetched.Zone);
            var parent = google.Parent is not null && links.TryGetValue(google.Parent, out var parentLink)
                && tasksById.TryGetValue(parentLink.TaskId, out var found) && !found.IsSubtask
                ? found
                : null;
            var task = parent is not null
                ? TaskItem.CreateSubtask(parent, GoogleTaskMapper.Title(google), google.Notes, dueAt, dueHasTime,
                    TaskPriority.None, 0, timeProvider)
                : TaskItem.Create(binding.UserId, binding.ListId, GoogleTaskMapper.Title(google), google.Notes, dueAt,
                    dueHasTime, TaskPriority.None, fetched.Zone, null, 0, timeProvider);
            if (google.IsCompleted)
                task.Complete(timeProvider);
            await tasks.AddAsync(task, ct);
            tasksById[task.Id] = task;

            var link = TaskLink.Create(binding.Id, task.Id, google.Id, timeProvider);
            link.Touch(google.Id, google.Etag, google.Updated);
            await linkRepository.AddAsync(link, ct);
            links[google.Id] = link;
        }

        private async Task UpdateAsync(TaskItem task, TaskLink link, GoogleTask google, CancellationToken ct)
        {
            var (dueAt, dueHasTime) = GoogleTaskMapper.Due(google, task, fetched.Zone);
            // A recurring task keeps the due date its recurrence hangs on.
            if (dueAt is null && task.IsRecurring)
                (dueAt, dueHasTime) = (task.DueAt, task.DueHasTime);
            task.Update(GoogleTaskMapper.Title(google), google.Notes, dueAt, dueHasTime, task.Priority);

            var done = task.Status is TaskItemStatus.Done or TaskItemStatus.Cancelled;
            TaskItem? spawned = null;
            if (google.IsCompleted && !done)
                spawned = await TaskCompletion.CompleteAsync(context, task, timeProvider, ct);
            else if (!google.IsCompleted && done)
                task.Reopen();
            await tasks.UpdateAsync(task, ct);

            // The next instance is Pandora's: it goes to Google on the next push.
            if (spawned is not null)
                await ExternalTaskLists.MarkChangedAsync(context, spawned, timeProvider, ct);

            link.Touch(google.Id, google.Etag, google.Updated);
            await linkRepository.UpdateAsync(link, ct);
        }

        /// <summary>Soft-deletes the task linked to a Google id and drops the link.</summary>
        private async Task RemoveAsync(string remoteTaskId, CancellationToken ct)
        {
            // A pending edit is the next push's to settle.
            if (!links.TryGetValue(remoteTaskId, out var link) || link.IsPending)
                return;
            links.Remove(remoteTaskId);

            if (tasksById.Remove(link.TaskId, out var task))
            {
                task.Delete(timeProvider);
                await tasks.UpdateAsync(task, ct);
            }
            await linkRepository.RemoveAsync(link, ct);
        }
    }
}
