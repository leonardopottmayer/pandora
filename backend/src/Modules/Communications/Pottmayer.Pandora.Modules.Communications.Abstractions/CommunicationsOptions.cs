namespace Pottmayer.Pandora.Modules.Communications.Abstractions;

/// <summary>Configuration for the Communications module (bound from the <c>Pandora:Communications</c> section).</summary>
public sealed class CommunicationsOptions
{
    public const string SectionName = "Pandora:Communications";

    // No settings yet — the section and binding exist so later phases (providers, sync cadence,
    // retention) have a home without touching the Host wiring.
}
