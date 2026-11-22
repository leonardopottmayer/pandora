using Pottmayer.Pandora.Shared.Domain;

namespace Pottmayer.Pandora.Modules.Agenda.Domain.ValueObjects;

/// <summary>What a sync conflict is about: an <c>event</c> (Google Calendar) or a <c>task</c> (Google Tasks).</summary>
public sealed record SyncConflictSubject : IDomainValue<SyncConflictSubject>
{
    public string Value { get; }

    private SyncConflictSubject(string value) => Value = value;

    public static readonly SyncConflictSubject Event = new("event");
    public static readonly SyncConflictSubject Task = new("task");

    public static readonly IReadOnlyList<SyncConflictSubject> All = [Event, Task];

    public static bool IsSupported(string? value) => All.Any(x => x.Value == value);

    public static SyncConflictSubject FromValue(string value) =>
        All.FirstOrDefault(x => x.Value == value)
        ?? throw new ArgumentException($"Unsupported sync conflict subject value: '{value}'.", nameof(value));

    public override string ToString() => Value;
}
