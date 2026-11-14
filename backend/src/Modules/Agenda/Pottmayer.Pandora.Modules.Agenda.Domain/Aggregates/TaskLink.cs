using Pottmayer.Tars.Core.Ddd;

namespace Pottmayer.Pandora.Modules.Agenda.Domain.Aggregates;

/// <summary>
/// One Pandora <see cref="TaskItem"/> linked to one Google task of a <see cref="TaskListBinding"/> (doc
/// agd014). Google picks a task's id, so a task born in Pandora has no <see cref="RemoteTaskId"/> until its
/// first insert.
/// </summary>
public sealed class TaskLink : AggregateRoot<Guid>
{
    public Guid BindingId { get; private set; }
    public Guid TaskId { get; private set; }
    public string? RemoteTaskId { get; private set; }

    /// <summary>Google's version of the task as last seen or sent.</summary>
    public string? Etag { get; private set; }

    public DateTimeOffset? RemoteUpdatedAt { get; private set; }

    /// <summary>When Pandora last changed the task without Google having it yet. Null ⇒ nothing to send.</summary>
    public DateTimeOffset? PendingPushAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    private TaskLink() { }

    public bool IsInGoogle => RemoteTaskId is not null;
    public bool IsPending => PendingPushAt is not null;

    public static TaskLink Create(Guid bindingId, Guid taskId, string? remoteTaskId, TimeProvider timeProvider) => new()
    {
        Id = Guid.CreateVersion7(),
        BindingId = bindingId,
        TaskId = taskId,
        RemoteTaskId = remoteTaskId,
        CreatedAt = timeProvider.GetUtcNow()
    };

    /// <summary>A task created in Pandora, waiting to be inserted in Google.</summary>
    public static TaskLink CreateLocal(Guid bindingId, Guid taskId, TimeProvider timeProvider)
    {
        var link = Create(bindingId, taskId, null, timeProvider);
        link.PendingPushAt = link.CreatedAt;
        return link;
    }

    public void Touch(string remoteTaskId, string? etag, DateTimeOffset? remoteUpdatedAt)
    {
        RemoteTaskId = remoteTaskId;
        Etag = etag;
        RemoteUpdatedAt = remoteUpdatedAt;
    }

    /// <summary>Pandora changed the task: send it on the next push.</summary>
    public void MarkPending(DateTimeOffset now) => PendingPushAt = now;

    /// <summary>Google has Pandora's version, or Google's newer edit won.</summary>
    public void ClearPending() => PendingPushAt = null;
}
