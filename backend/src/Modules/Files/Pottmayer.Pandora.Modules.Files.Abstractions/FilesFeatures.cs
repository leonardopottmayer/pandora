namespace Pottmayer.Pandora.Modules.Files.Abstractions;

/// <summary>
/// Feature flag keys of the Files module. The module declares them in its <c>ApplicationDI</c>.
/// </summary>
public static class FilesFeatures
{
    /// <summary>Gates the whole module.</summary>
    public const string Files = FilesModule.Name;
}
