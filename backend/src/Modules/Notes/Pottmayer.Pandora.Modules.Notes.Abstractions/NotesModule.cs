namespace Pottmayer.Pandora.Modules.Notes.Abstractions;

/// <summary>
/// Cross-cutting identity of the Notes module, shared across its layers.
/// </summary>
public static class NotesModule
{
    /// <summary>Logical name of the module. Also used as the database routing key.</summary>
    public const string Name = "notes";

    /// <summary>Database pipeline key (Tars) for this module.</summary>
    public const string DatabaseKey = Name;

    /// <summary>Database schema that owns this module's tables.</summary>
    public const string Schema = Name;
}
