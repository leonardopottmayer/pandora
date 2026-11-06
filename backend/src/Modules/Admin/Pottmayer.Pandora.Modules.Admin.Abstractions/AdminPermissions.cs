using Pottmayer.Tars.Security.Authorization.Abstractions;

namespace Pottmayer.Pandora.Modules.Admin.Abstractions;

/// <summary>
/// Permission keys of the Admin module (<c>admin.resource.action</c>). The module declares them in its
/// <c>ApplicationDI</c>; requests, Assistant tools and checks reference these constants instead of strings.
/// </summary>
public static class AdminPermissions
{
    /// <summary>Module segment of every key.</summary>
    public const string Module = AdminModule.Name;

    /// <summary>Actions of this module beyond the standard ones in <see cref="PermissionActions"/>.</summary>
    public static class Actions
    {
        public const string Manage = "manage";
    }

    public static class Users
    {
        public const string Resource = "users";

        public const string Read = Module + "." + Resource + "." + PermissionActions.Read;
        public const string Manage = Module + "." + Resource + "." + Actions.Manage;
    }

    public static class Roles
    {
        public const string Resource = "roles";

        public const string Read = Module + "." + Resource + "." + PermissionActions.Read;
        public const string Manage = Module + "." + Resource + "." + Actions.Manage;
    }

    public static class Features
    {
        public const string Resource = "features";

        public const string Manage = Module + "." + Resource + "." + Actions.Manage;
    }

    public static class Audit
    {
        public const string Resource = "audit";

        public const string Read = Module + "." + Resource + "." + PermissionActions.Read;
    }
}
