namespace Pottmayer.Pandora.Modules.Integrations.Abstractions;

/// <summary>
/// Feature flag keys of the Integrations module. The module declares them in its <c>ApplicationDI</c>.
/// </summary>
public static class IntegrationsFeatures
{
    /// <summary>Gates the whole module.</summary>
    public const string Integrations = IntegrationsModule.Name;
}
