namespace Pottmayer.Pandora.Modules.Communications.Abstractions;

/// <summary>
/// Feature flag keys of the Communications module. The module declares them in its <c>ApplicationDI</c>.
/// </summary>
public static class CommunicationsFeatures
{
    /// <summary>Gates the whole module.</summary>
    public const string Communications = CommunicationsModule.Name;
}
