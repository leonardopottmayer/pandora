namespace Pottmayer.Pandora.Modules.Agenda.Domain.ValueObjects;

/// <summary>
/// What an <see cref="Aggregates.Alert"/> is attached to. The column is polymorphic by design
/// (doc agd007), so all three are declared; Phase 3 only wires <see cref="Task"/>. Events and
/// reminders are handled in later phases.
/// </summary>
public enum AlertSubjectType
{
    Task,
    Event,
    Reminder,
}
