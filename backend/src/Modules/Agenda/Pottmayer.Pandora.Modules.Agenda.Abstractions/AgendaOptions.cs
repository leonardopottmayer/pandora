namespace Pottmayer.Pandora.Modules.Agenda.Abstractions;

/// <summary>Configuration for the Agenda module (bound from the <c>Pandora:Agenda</c> section).</summary>
public sealed class AgendaOptions
{
    public const string SectionName = "Pandora:Agenda";

    /// <summary>How often the sweep scans for due reminders.</summary>
    public int SweepIntervalSeconds { get; set; } = 30;

    /// <summary>How many reminders the sweep fires per tick.</summary>
    public int SweepBatchSize { get; set; } = 50;
}
