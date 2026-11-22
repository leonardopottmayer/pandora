namespace Pottmayer.Pandora.Modules.Channels.Abstractions;

/// <summary>
/// Feature flag keys of the Channels module. The module declares them in its <c>ApplicationDI</c>.
/// </summary>
public static class ChannelsFeatures
{
    /// <summary>Gates the whole module.</summary>
    public const string Channels = ChannelsModule.Name;
}
