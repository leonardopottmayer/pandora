using Pottmayer.Pandora.Modules.Agenda.Domain.ValueObjects;
using Pottmayer.Pandora.Shared.Domain;
using Pottmayer.Tars.Core.Ddd;

namespace Pottmayer.Pandora.Modules.Agenda.Domain.Aggregates;

/// <summary>
/// One Pandora <see cref="TaskList"/> bound to one Google Tasks list of one Integrations account (doc
/// agd012), always both ways. Google Tasks has no sync token: <see cref="UpdatedCursor"/> is when the last
/// listing started, and the next asks for what changed since.
/// </summary>
public sealed class TaskListBinding : AggregateRoot<Guid>, IAuditable
{
    private const int MaxErrorLength = 1000;

    public Guid UserId { get; private set; }
    public Guid ListId { get; private set; }
    public Guid ExternalAccountId { get; private set; }
    public string RemoteListId { get; private set; } = string.Empty;

    /// <summary>Google's <c>updatedMin</c> for the next listing. Null ⇒ the next cycle lists everything.</summary>
    public DateTimeOffset? UpdatedCursor { get; private set; }

    public DateTimeOffset? FullSyncedAt { get; private set; }
    public DateTimeOffset? LastSyncedAt { get; private set; }
    public CalendarBindingStatus Status { get; private set; } = CalendarBindingStatus.Ok;
    public int FailureCount { get; private set; }
    public string? LastError { get; private set; }

    public Guid? CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }

    private TaskListBinding() { }

    public static TaskListBinding Create(
        Guid userId, Guid listId, Guid externalAccountId, string remoteListId, TimeProvider timeProvider)
    {
        if (string.IsNullOrWhiteSpace(remoteListId))
            throw new ArgumentException("A binding needs the remote task list's id.", nameof(remoteListId));

        return new TaskListBinding
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            ListId = listId,
            ExternalAccountId = externalAccountId,
            RemoteListId = remoteListId,
            CreatedAt = timeProvider.GetUtcNow()
        };
    }

    /// <summary>True when the next cycle must list every task instead of the changes since <see cref="UpdatedCursor"/>.</summary>
    public bool NeedsFullSync(DateTimeOffset now, TimeSpan fullSyncEvery) =>
        UpdatedCursor is null || FullSyncedAt is null || now - FullSyncedAt.Value >= fullSyncEvery;

    /// <summary>A cycle finished: the next listing asks for what changed since <paramref name="cursor"/>.</summary>
    public void RecordSuccess(DateTimeOffset cursor, bool wasFullSync, DateTimeOffset now)
    {
        UpdatedCursor = cursor;
        if (wasFullSync)
            FullSyncedAt = now;
        LastSyncedAt = now;
        Status = CalendarBindingStatus.Ok;
        FailureCount = 0;
        LastError = null;
    }

    /// <summary>A cycle failed for a reason that may pass; after <see cref="CalendarBinding.MaxConsecutiveFailures"/> the binding stops.</summary>
    public void RecordFailure(string error)
    {
        LastError = Trim(error);
        FailureCount++;
        if (FailureCount >= CalendarBinding.MaxConsecutiveFailures)
            Status = CalendarBindingStatus.Error;
    }

    public void MarkNeedsReconnect(string error)
    {
        LastError = Trim(error);
        Status = CalendarBindingStatus.NeedsReconnect;
    }

    /// <summary>Some of Pandora's edits did not reach Google this cycle; shown, not counted.</summary>
    public void NotePushFailure(string error) => LastError = Trim(error);

    public void Resume()
    {
        Status = CalendarBindingStatus.Ok;
        FailureCount = 0;
    }

    /// <summary>The next listing is a full one (Google's newer edit won a conflict).</summary>
    public void ResetCursor() => UpdatedCursor = null;

    private static string Trim(string error) => error.Length <= MaxErrorLength ? error : error[..MaxErrorLength];
}
