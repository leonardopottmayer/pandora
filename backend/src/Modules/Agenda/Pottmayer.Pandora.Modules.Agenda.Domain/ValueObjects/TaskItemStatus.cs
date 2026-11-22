namespace Pottmayer.Pandora.Modules.Agenda.Domain.ValueObjects;

/// <summary>Lifecycle of a task. Named <c>TaskItemStatus</c> to avoid clashing with <see cref="System.Threading.Tasks.TaskStatus"/>.</summary>
public enum TaskItemStatus
{
    /// <summary>Not started.</summary>
    Todo,

    /// <summary>Started, not finished.</summary>
    InProgress,

    /// <summary>Finished. <c>CompletedAt</c> is set; a recurring task spawns its next instance on this transition.</summary>
    Done,

    /// <summary>Abandoned. Terminal, and not resurrected by recurrence.</summary>
    Cancelled,
}
