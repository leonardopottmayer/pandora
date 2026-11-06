using Pottmayer.Tars.Security.Authorization.Abstractions;

namespace Pottmayer.Pandora.Modules.Assistant.Abstractions;

/// <summary>
/// Permission keys of the Assistant module (<c>assistant.resource.action</c>). The module declares them in its
/// <c>ApplicationDI</c>; requests, Assistant tools and checks reference these constants instead of strings.
/// </summary>
public static class AssistantPermissions
{
    /// <summary>Module segment of every key.</summary>
    public const string Module = AssistantModule.Name;

    /// <summary>Actions of this module beyond the standard ones in <see cref="PermissionActions"/>.</summary>
    public static class Actions
    {
        public const string Run = "run";
    }

    public static class Commands
    {
        public const string Resource = "commands";

        public const string Read = Module + "." + Resource + "." + PermissionActions.Read;
        public const string Run = Module + "." + Resource + "." + Actions.Run;
    }

    public static class Profile
    {
        public const string Resource = "profile";

        public const string Read = Module + "." + Resource + "." + PermissionActions.Read;
        public const string Update = Module + "." + Resource + "." + PermissionActions.Update;
    }
}
