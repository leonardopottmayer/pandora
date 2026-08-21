namespace Pottmayer.Pandora.Modules.Agenda.Abstractions;

/// <summary>Configuration for the Agenda module (bound from the <c>Pandora:Agenda</c> section).</summary>
public sealed class AgendaOptions
{
    public const string SectionName = "Pandora:Agenda";

    /// <summary>How often the sweep scans for due reminders.</summary>
    public int SweepIntervalSeconds { get; set; } = 30;

    /// <summary>How many reminders the sweep fires per tick.</summary>
    public int SweepBatchSize { get; set; } = 50;

    /// <summary>
    /// How far back the sweep looks for missed recurring occurrences. Covers a suspended machine: an
    /// occurrence within this window is still delivered once (marked late); one older than it is
    /// missed rather than replayed, so waking up does not flood.
    /// </summary>
    public int SweepGraceMinutes { get; set; } = 15;
}
