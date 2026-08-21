namespace Pottmayer.Pandora.Modules.Agenda.Domain.ValueObjects;

/// <summary>
/// Where a <see cref="Aggregates.Calendar"/> came from. A <see cref="Local"/> calendar is owned here;
/// an <see cref="External"/> one was born from a provider pull and is mostly read-only. Only
/// <see cref="Local"/> matters until Google sync (Phase 5).
/// </summary>
public enum CalendarOrigin
{
    Local,
    External,
}
