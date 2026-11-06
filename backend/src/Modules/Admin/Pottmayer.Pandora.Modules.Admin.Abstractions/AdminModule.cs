namespace Pottmayer.Pandora.Modules.Admin.Abstractions;

/// <summary>
/// Cross-cutting identity of the Admin module, shared across its layers.
/// </summary>
/// <remarks>
/// Admin decides what is switched on in the system (feature flags) and what each user may do (roles and
/// per-user exceptions). Other modules never reference it: they declare their flags and permissions and
/// mark their requests through the Tars abstractions; Admin supplies the stores behind them.
/// </remarks>
public static class AdminModule
{
    /// <summary>Logical name of the module. Also used as the database routing key.</summary>
    public const string Name = "admin";

    /// <summary>Database pipeline key (Tars) for this module.</summary>
    public const string DatabaseKey = Name;

    /// <summary>Database schema that owns this module's tables.</summary>
    public const string Schema = Name;

    /// <summary>The <c>admin</c> system role (holds <c>*</c>), seeded by the first migration with this id.</summary>
    public static readonly Guid AdminRoleId = Guid.Parse("019a5a00-0000-7000-8000-000000000001");
}
