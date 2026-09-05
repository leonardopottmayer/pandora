namespace Pottmayer.Pandora.Modules.Assistant.Abstractions;

/// <summary>Configuration for the Assistant module (bound from the <c>Pandora:Assistant</c> section).</summary>
public sealed class AssistantOptions
{
    public const string SectionName = "Pandora:Assistant";

    /// <summary>
    /// How many prior messages of the active conversation are re-sent to the model as context on each
    /// turn, so a follow-up ("sim", "muda pra 11h") is understood. Caps token cost and how much of the
    /// conversation leaves for the provider; the active-conversation window (idle timeout) bounds it in
    /// time. Default 10.
    /// </summary>
    public int HistoryMessageLimit { get; set; } = 10;
}
