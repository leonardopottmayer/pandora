namespace Pottmayer.Pandora.Modules.Communications.Abstractions;

/// <summary>
/// Cross-cutting identity of the Communications module, shared across its layers.
/// </summary>
/// <remarks>
/// Communications is the "inbox" domain — the user's messaging surface (email first, via the Gmail
/// provider that plugs into Integrations, other channels later). It is deliberately distinct from
/// Channels, which is the module's <b>outbound</b> notification transport.
/// </remarks>
public static class CommunicationsModule
{
    /// <summary>Logical name of the module. Also used as the database routing key.</summary>
    public const string Name = "communications";

    /// <summary>Database pipeline key (Tars) for this module.</summary>
    public const string DatabaseKey = Name;

    /// <summary>Database schema that owns this module's tables.</summary>
    public const string Schema = Name;
}
