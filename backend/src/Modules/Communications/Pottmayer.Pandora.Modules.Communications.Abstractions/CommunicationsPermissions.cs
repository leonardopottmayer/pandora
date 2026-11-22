using Pottmayer.Tars.Security.Authorization.Abstractions;

namespace Pottmayer.Pandora.Modules.Communications.Abstractions;

/// <summary>
/// Permission keys of the Communications module (<c>communications.resource.action</c>). The module declares them in its
/// <c>ApplicationDI</c>; requests, Assistant tools and checks reference these constants instead of strings.
/// </summary>
public static class CommunicationsPermissions
{
    /// <summary>Module segment of every key.</summary>
    public const string Module = CommunicationsModule.Name;

    /// <summary>Actions of this module beyond the standard ones in <see cref="PermissionActions"/>.</summary>
    public static class Actions
    {
        public const string Send = "send";
    }

    public static class Mailboxes
    {
        public const string Resource = "mailboxes";

        public const string Read = Module + "." + Resource + "." + PermissionActions.Read;
        public const string Create = Module + "." + Resource + "." + PermissionActions.Create;
        public const string Update = Module + "." + Resource + "." + PermissionActions.Update;
        public const string Delete = Module + "." + Resource + "." + PermissionActions.Delete;
    }

    public static class Messages
    {
        public const string Resource = "messages";

        public const string Read = Module + "." + Resource + "." + PermissionActions.Read;
        public const string Update = Module + "." + Resource + "." + PermissionActions.Update;
        public const string Send = Module + "." + Resource + "." + Actions.Send;
    }

    public static class Labels
    {
        public const string Resource = "labels";

        public const string Read = Module + "." + Resource + "." + PermissionActions.Read;
        public const string Update = Module + "." + Resource + "." + PermissionActions.Update;
    }

    public static class Rules
    {
        public const string Resource = "rules";

        public const string Read = Module + "." + Resource + "." + PermissionActions.Read;
        public const string Update = Module + "." + Resource + "." + PermissionActions.Update;
    }
}
