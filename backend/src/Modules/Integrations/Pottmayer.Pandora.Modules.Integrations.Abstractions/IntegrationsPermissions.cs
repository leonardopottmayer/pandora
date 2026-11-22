using Pottmayer.Tars.Security.Authorization.Abstractions;

namespace Pottmayer.Pandora.Modules.Integrations.Abstractions;

/// <summary>
/// Permission keys of the Integrations module (<c>integrations.resource.action</c>). The module declares them in its
/// <c>ApplicationDI</c>; requests, Assistant tools and checks reference these constants instead of strings.
/// </summary>
public static class IntegrationsPermissions
{
    /// <summary>Module segment of every key.</summary>
    public const string Module = IntegrationsModule.Name;

    public static class Accounts
    {
        public const string Resource = "accounts";

        public const string Read = Module + "." + Resource + "." + PermissionActions.Read;
        public const string Create = Module + "." + Resource + "." + PermissionActions.Create;
        public const string Update = Module + "." + Resource + "." + PermissionActions.Update;
        public const string Delete = Module + "." + Resource + "." + PermissionActions.Delete;
    }
}
