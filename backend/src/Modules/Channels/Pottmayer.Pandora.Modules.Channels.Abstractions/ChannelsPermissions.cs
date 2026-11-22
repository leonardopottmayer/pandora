using Pottmayer.Tars.Security.Authorization.Abstractions;

namespace Pottmayer.Pandora.Modules.Channels.Abstractions;

/// <summary>
/// Permission keys of the Channels module (<c>channels.resource.action</c>). The module declares them in its
/// <c>ApplicationDI</c>; requests, Assistant tools and checks reference these constants instead of strings.
/// </summary>
public static class ChannelsPermissions
{
    /// <summary>Module segment of every key.</summary>
    public const string Module = ChannelsModule.Name;

    /// <summary>Actions of this module beyond the standard ones in <see cref="PermissionActions"/>.</summary>
    public static class Actions
    {
        public const string Test = "test";
    }

    public static class Channels
    {
        public const string Resource = "channels";

        public const string Read = Module + "." + Resource + "." + PermissionActions.Read;
        public const string Create = Module + "." + Resource + "." + PermissionActions.Create;
        public const string Delete = Module + "." + Resource + "." + PermissionActions.Delete;
        public const string Test = Module + "." + Resource + "." + Actions.Test;
    }

    public static class Notifications
    {
        public const string Resource = "notifications";

        public const string Read = Module + "." + Resource + "." + PermissionActions.Read;
        public const string Update = Module + "." + Resource + "." + PermissionActions.Update;
    }
}
