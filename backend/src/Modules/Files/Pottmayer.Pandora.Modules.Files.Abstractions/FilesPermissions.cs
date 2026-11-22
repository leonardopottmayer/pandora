using Pottmayer.Tars.Security.Authorization.Abstractions;

namespace Pottmayer.Pandora.Modules.Files.Abstractions;

/// <summary>
/// Permission keys of the Files module (<c>files.resource.action</c>). The module declares them in its
/// <c>ApplicationDI</c>; requests, Assistant tools and checks reference these constants instead of strings.
/// </summary>
public static class FilesPermissions
{
    /// <summary>Module segment of every key.</summary>
    public const string Module = FilesModule.Name;

    /// <summary>Actions of this module beyond the standard ones in <see cref="PermissionActions"/>.</summary>
    public static class Actions
    {
        public const string Run = "run";
    }

    public static class Roots
    {
        public const string Resource = "roots";

        public const string Read = Module + "." + Resource + "." + PermissionActions.Read;
        public const string Create = Module + "." + Resource + "." + PermissionActions.Create;
        public const string Update = Module + "." + Resource + "." + PermissionActions.Update;
        public const string Delete = Module + "." + Resource + "." + PermissionActions.Delete;
    }

    public static class Scans
    {
        public const string Resource = "scans";

        public const string Read = Module + "." + Resource + "." + PermissionActions.Read;
        public const string Run = Module + "." + Resource + "." + Actions.Run;
    }

    public static class Entries
    {
        public const string Resource = "entries";

        public const string Read = Module + "." + Resource + "." + PermissionActions.Read;
    }

    public static class Review
    {
        public const string Resource = "review";

        public const string Read = Module + "." + Resource + "." + PermissionActions.Read;
        public const string Update = Module + "." + Resource + "." + PermissionActions.Update;
    }

    public static class Filters
    {
        public const string Resource = "filters";

        public const string Read = Module + "." + Resource + "." + PermissionActions.Read;
        public const string Update = Module + "." + Resource + "." + PermissionActions.Update;
        public const string Delete = Module + "." + Resource + "." + PermissionActions.Delete;
    }

    public static class Preferences
    {
        public const string Resource = "preferences";

        public const string Read = Module + "." + Resource + "." + PermissionActions.Read;
        public const string Update = Module + "." + Resource + "." + PermissionActions.Update;
    }
}
