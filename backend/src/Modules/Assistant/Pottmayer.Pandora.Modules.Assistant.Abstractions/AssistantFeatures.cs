namespace Pottmayer.Pandora.Modules.Assistant.Abstractions;

/// <summary>
/// Feature flag keys of the Assistant module. The module declares them in its <c>ApplicationDI</c>.
/// </summary>
public static class AssistantFeatures
{
    /// <summary>Gates the whole module.</summary>
    public const string Assistant = AssistantModule.Name;
}
