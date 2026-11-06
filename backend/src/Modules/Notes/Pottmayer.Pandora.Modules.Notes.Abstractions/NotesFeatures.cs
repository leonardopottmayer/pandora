namespace Pottmayer.Pandora.Modules.Notes.Abstractions;

/// <summary>
/// Feature flag keys of the Notes module. The module declares them in its <c>ApplicationDI</c>.
/// </summary>
public static class NotesFeatures
{
    /// <summary>Gates the whole module.</summary>
    public const string Notes = NotesModule.Name;
}
