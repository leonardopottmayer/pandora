namespace Pottmayer.Pandora.Modules.Files.Abstractions;

/// <summary>
/// Cross-cutting identity of the Files module, shared across its layers.
/// </summary>
/// <remarks>
/// Files is the catalog of what is on the user's disks: agents on paired devices walk the roots the user
/// chose and report what they see; the backend keeps the catalog and never holds a file's bytes.
/// </remarks>
public static class FilesModule
{
    /// <summary>Logical name of the module. Also used as the database routing key.</summary>
    public const string Name = "files";

    /// <summary>Database pipeline key (Tars) for this module.</summary>
    public const string DatabaseKey = Name;

    /// <summary>Database schema that owns this module's tables.</summary>
    public const string Schema = Name;
}
