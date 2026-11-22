using Pottmayer.Tars.Core.Primitives.Outcomes;

namespace Pottmayer.Pandora.Modules.Agenda.Application.Google;

/// <summary>A task list of the Google account (<c>tasklists.list</c>).</summary>
public sealed record GoogleTaskListEntry(string Id, string? Title);

/// <summary>
/// A task as Google Tasks API v1 returns it. <see cref="Due"/> carries a date only (Google drops the time);
/// <see cref="Status"/> is <c>needsAction</c> or <c>completed</c>.
/// </summary>
public sealed record GoogleTask(
    string Id,
    string? Etag,
    string? Title,
    string? Notes,
    string? Status,
    string? Due,
    string? Completed,
    string? Parent,
    DateTimeOffset? Updated,
    bool? Deleted)
{
    public bool IsCompleted => Status == "completed";
    public bool IsDeleted => Deleted == true;
}

/// <summary>
/// What Pandora sends: every field Google Tasks has and Pandora holds, nulls included, because an update
/// replaces the task (a null due date clears it).
/// </summary>
public sealed record GoogleTaskWrite(string? Id, string Title, string? Notes, string Status, string? Due, string? Completed);

/// <summary>Google Tasks API v1 with the access token of an Integrations account. Fails with errors, never throws.</summary>
public interface IGoogleTasksClient
{
    Task<Result<IReadOnlyList<GoogleTaskListEntry>>> ListTaskListsAsync(Guid userId, Guid accountId, CancellationToken ct = default);

    /// <summary>
    /// Every page of <c>tasks.list</c>, completed and hidden tasks included. With <paramref name="updatedMin"/>,
    /// only what changed since, deleted tasks included; without, every live task.
    /// </summary>
    Task<Result<IReadOnlyList<GoogleTask>>> ListTasksAsync(
        Guid userId, Guid accountId, string listId, DateTimeOffset? updatedMin, CancellationToken ct = default);

    /// <summary>One task. Missing ⇒ <see cref="Errors.GoogleTasksErrors.TaskNotFound"/>.</summary>
    Task<Result<GoogleTask>> GetTaskAsync(Guid userId, Guid accountId, string listId, string taskId, CancellationToken ct = default);

    /// <summary>Inserts a task, under <paramref name="parentId"/> when it is a subtask. Google picks the id.</summary>
    Task<Result<GoogleTask>> InsertTaskAsync(
        Guid userId, Guid accountId, string listId, string? parentId, GoogleTaskWrite body, CancellationToken ct = default);

    /// <summary>Replaces a task's fields. Missing ⇒ <see cref="Errors.GoogleTasksErrors.TaskNotFound"/>.</summary>
    Task<Result<GoogleTask>> UpdateTaskAsync(
        Guid userId, Guid accountId, string listId, string taskId, GoogleTaskWrite body, CancellationToken ct = default);

    /// <summary>Deletes a task. Already gone counts as done.</summary>
    Task<Result<bool>> DeleteTaskAsync(Guid userId, Guid accountId, string listId, string taskId, CancellationToken ct = default);
}
