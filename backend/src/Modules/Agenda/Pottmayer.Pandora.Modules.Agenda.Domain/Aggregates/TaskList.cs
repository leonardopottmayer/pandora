using Pottmayer.Pandora.Shared.Domain;
using Pottmayer.Tars.Core.Ddd;

namespace Pottmayer.Pandora.Modules.Agenda.Domain.Aggregates;

/// <summary>
/// A named container of tasks (the Apple Reminders "list", the Todoist "project"). Every user has at
/// least one; exactly one is the default, guarded by a partial unique index. Archiving hides it
/// without deleting.
/// </summary>
public sealed class TaskList : AggregateRoot<Guid>, IAuditable
{
    private const int MaxNameLength = 200;

    public Guid UserId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public bool IsDefault { get; private set; }

    /// <summary>Manual ordering in the sidebar.</summary>
    public int Position { get; private set; }

    /// <summary>Soft hide; an archived list still owns its tasks.</summary>
    public DateTimeOffset? ArchivedAt { get; private set; }

    public Guid? CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }

    private TaskList() { }

    public static TaskList Create(Guid userId, string name, bool isDefault, int position, TimeProvider timeProvider)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("A task list needs a name.", nameof(name));

        return new TaskList
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            Name = Trim(name),
            IsDefault = isDefault,
            Position = position,
            CreatedAt = timeProvider.GetUtcNow()
        };
    }

    public void Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("A task list needs a name.", nameof(name));

        Name = Trim(name);
    }

    public void SetPosition(int position) => Position = position;

    public void SetDefault(bool isDefault) => IsDefault = isDefault;

    public void Archive(TimeProvider timeProvider) => ArchivedAt ??= timeProvider.GetUtcNow();

    private static string Trim(string name) => name.Trim() is { Length: > MaxNameLength } t ? t[..MaxNameLength] : name.Trim();
}
