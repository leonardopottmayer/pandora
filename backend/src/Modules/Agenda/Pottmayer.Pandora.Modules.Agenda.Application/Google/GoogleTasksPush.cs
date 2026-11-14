using System.Text.Json;
using Pottmayer.Pandora.Modules.Agenda.Abstractions;
using Pottmayer.Pandora.Modules.Agenda.Application.Errors;
using Pottmayer.Pandora.Modules.Agenda.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Agenda.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Agenda.Domain.ValueObjects;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Agenda.Application.Google;

/// <summary>
/// Pandora → Google Tasks, the first half of a cycle: every task whose link is pending is inserted,
/// updated or deleted, and the link takes Google's new etag, which is how the reading half recognises the
/// change coming back.
/// </summary>
/// <remarks>
/// Google Tasks takes no <c>If-Match</c>, so an update first reads the task: an etag other than the link's
/// means it changed in Google too. The newer edit wins (Google's <c>updated</c> against the time of
/// Pandora's edit) and the other version goes to the conflict log; when Google wins, the cursor is reset so
/// the reading half lists everything and brings Google's version.
/// </remarks>
internal sealed class GoogleTasksPush(IUnitOfWorkFactory factory, IGoogleTasksClient google, TimeProvider timeProvider)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary><see cref="Fatal"/>: the account cannot be used. <see cref="Failure"/>: some tasks did not go and stay pending.</summary>
    public sealed record Outcome(Error? Fatal, string? Failure);

    private sealed record Item(TaskLink Link, TaskItem? Task);

    private sealed record Done(
        GoogleTask? Remote = null,
        bool Removed = false,
        bool DeletedInGoogle = false,
        bool GoogleWon = false,
        bool KeepPending = false,
        SyncConflict? Conflict = null);

    public async Task<Outcome> PushAsync(TaskListBinding binding, CancellationToken ct)
    {
        var (items, remoteIds) = await LoadAsync(binding.Id, ct);

        string? failure = null;
        foreach (var item in items)
        {
            var result = await PushOneAsync(binding, item, remoteIds, ct);
            if (!result.IsSuccess)
            {
                var error = result.Errors[0];
                if (GoogleCalendarErrors.NeedsReconnect(error))
                    return new Outcome(error, null);
                failure ??= $"\"{item.Task?.Title}\" did not reach Google: {error.Message}";
                continue;
            }

            if (result.Value!.Remote is { } remote)
                remoteIds[item.Link.TaskId] = remote.Id;
            await SaveAsync(binding.Id, item.Link, result.Value, ct);
        }

        return new Outcome(null, failure);
    }

    /// <summary>The pending tasks, parents before subtasks, and the Google id of every linked task (a subtask's insert names its parent's).</summary>
    private Task<(IReadOnlyList<Item>, Dictionary<Guid, string>)> LoadAsync(Guid bindingId, CancellationToken ct) =>
        factory.ExecuteAsync(AgendaModule.DatabaseKey, async (context, token) =>
        {
            var links = context.AcquireRepository<ITaskLinkRepository>();
            var remoteIds = (await links.GetByBindingAsync(bindingId, token))
                .Where(l => l.IsInGoogle).ToDictionary(l => l.TaskId, l => l.RemoteTaskId!);

            var pending = await links.GetPendingAsync(bindingId, token);
            if (pending.Count == 0)
                return ((IReadOnlyList<Item>)[], remoteIds);

            var tasks = (await context.AcquireRepository<ITaskRepository>()
                    .GetByIdsAsync([.. pending.Select(l => l.TaskId)], token))
                .ToDictionary(t => t.Id);
            IReadOnlyList<Item> items =
            [
                .. pending.Select(l => new Item(l, tasks.GetValueOrDefault(l.TaskId)))
                    .OrderBy(i => i.Task?.IsSubtask == true)
            ];
            return (items, remoteIds);
        }, cancellationToken: ct);

    private async Task<Result<Done>> PushOneAsync(
        TaskListBinding binding, Item item, IReadOnlyDictionary<Guid, string> remoteIds, CancellationToken ct)
    {
        var (link, task) = (item.Link, item.Task);
        var (user, account, list) = (binding.UserId, binding.ExternalAccountId, binding.RemoteListId);

        if (task is null || task.DeletedAt is not null)
        {
            if (!link.IsInGoogle)
                return Result<Done>.Success(new Done(Removed: true));
            var deleted = await google.DeleteTaskAsync(user, account, list, link.RemoteTaskId!, ct);
            return deleted.IsSuccess ? Result<Done>.Success(new Done(Removed: true)) : Result<Done>.Failure(deleted.Errors);
        }

        if (!link.IsInGoogle)
        {
            string? parentId = null;
            if (task.ParentTaskId is { } parent && !remoteIds.TryGetValue(parent, out parentId))
                return Result<Done>.Success(new Done(KeepPending: true)); // its parent did not reach Google yet

            // ponytail: Google picks the id, so an insert whose answer was lost makes a second task on retry;
            // look the task up by a marker in its notes if that ever shows up.
            var inserted = await google.InsertTaskAsync(user, account, list, parentId, GoogleTaskMapper.ToGoogle(task, null), ct);
            return inserted.IsSuccess ? Result<Done>.Success(new Done(inserted.Value)) : Result<Done>.Failure(inserted.Errors);
        }

        var remoteId = link.RemoteTaskId!;
        var body = GoogleTaskMapper.ToGoogle(task, remoteId);
        var current = await google.GetTaskAsync(user, account, list, remoteId, ct);
        if (IsNotFound(current) || current.Value?.IsDeleted == true)
            return Result<Done>.Success(new Done(Removed: true, DeletedInGoogle: true));
        if (!current.IsSuccess)
            return Result<Done>.Failure(current.Errors);

        SyncConflict? conflict = null;
        if (current.Value!.Etag != link.Etag)
        {
            if (current.Value.Updated is { } googleAt && googleAt > link.PendingPushAt)
                return Result<Done>.Success(new Done(GoogleWon: true,
                    Conflict: ConflictOf(binding, task, SyncConflictSide.Pandora, JsonSerializer.Serialize(body, Json))));
            conflict = ConflictOf(binding, task, SyncConflictSide.Google, JsonSerializer.Serialize(current.Value, Json));
        }

        var updated = await google.UpdateTaskAsync(user, account, list, remoteId, body, ct);
        if (IsNotFound(updated))
            return Result<Done>.Success(new Done(Removed: true, DeletedInGoogle: true));
        return updated.IsSuccess
            ? Result<Done>.Success(new Done(updated.Value, Conflict: conflict))
            : Result<Done>.Failure(updated.Errors);
    }

    private Task SaveAsync(Guid bindingId, TaskLink pushed, Done done, CancellationToken ct) =>
        factory.ExecuteAsync(AgendaModule.DatabaseKey, async (context, token) =>
        {
            var links = context.AcquireRepository<ITaskLinkRepository>();
            var link = await links.FindByTaskAsync(pushed.TaskId, token);
            if (link is null)
                return;

            if (done.Conflict is not null)
                await context.AcquireRepository<ISyncConflictRepository>().AddAsync(done.Conflict, token);

            if (done.DeletedInGoogle)
            {
                // Deleted in Google while Pandora edited it: the deletion stands.
                var tasks = context.AcquireRepository<ITaskRepository>();
                if ((await tasks.GetByIdsAsync([link.TaskId], token)).FirstOrDefault() is { DeletedAt: null } task)
                {
                    task.Delete(timeProvider);
                    await tasks.UpdateAsync(task, token);
                }
                await links.RemoveAsync(link, token);
                return;
            }

            // Edited again while this push was on the wire: that edit still has to go.
            var editedSince = link.PendingPushAt > pushed.PendingPushAt;

            if (done.Removed && !editedSince)
            {
                await links.RemoveAsync(link, token);
                return;
            }

            if (done.Remote is { } remote)
                link.Touch(remote.Id, remote.Etag, remote.Updated);
            if (!editedSince && !done.KeepPending)
                link.ClearPending();
            await links.UpdateAsync(link, token);

            if (done.GoogleWon)
            {
                var bindings = context.AcquireRepository<ITaskListBindingRepository>();
                if (await bindings.GetAsync(bindingId, token) is { } binding)
                {
                    binding.ResetCursor();
                    await bindings.UpdateAsync(binding, token);
                }
            }
        }, cancellationToken: ct);

    private SyncConflict ConflictOf(TaskListBinding binding, TaskItem task, SyncConflictSide losing, string version) =>
        SyncConflict.Create(binding.UserId, binding.Id, SyncConflictSubject.Task, task.Id, task.Title, losing, version, timeProvider);

    private static bool IsNotFound<T>(Result<T> result) =>
        !result.IsSuccess && result.Errors[0].Code == GoogleTasksErrors.TaskNotFound.Code;
}
