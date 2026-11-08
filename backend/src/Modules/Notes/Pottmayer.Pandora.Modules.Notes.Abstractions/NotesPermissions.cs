using Pottmayer.Tars.Security.Authorization.Abstractions;

namespace Pottmayer.Pandora.Modules.Notes.Abstractions;

/// <summary>
/// Permission keys of the Notes module (<c>notes.resource.action</c>). The module declares them in its
/// <c>ApplicationDI</c>; requests, Assistant tools and checks reference these constants instead of strings.
/// </summary>
public static class NotesPermissions
{
    /// <summary>Module segment of every key.</summary>
    public const string Module = NotesModule.Name;

    public static class Spaces
    {
        public const string Resource = "spaces";

        public const string Read = Module + "." + Resource + "." + PermissionActions.Read;
        public const string Create = Module + "." + Resource + "." + PermissionActions.Create;
        public const string Update = Module + "." + Resource + "." + PermissionActions.Update;
        public const string Delete = Module + "." + Resource + "." + PermissionActions.Delete;
    }

    public static class Pages
    {
        public const string Resource = "pages";

        public const string Read = Module + "." + Resource + "." + PermissionActions.Read;
        public const string Create = Module + "." + Resource + "." + PermissionActions.Create;
        public const string Update = Module + "." + Resource + "." + PermissionActions.Update;
        public const string Delete = Module + "." + Resource + "." + PermissionActions.Delete;
    }

    public static class Preferences
    {
        public const string Resource = "preferences";
        public const string Read = Module + "." + Resource + "." + PermissionActions.Read;
        public const string Update = Module + "." + Resource + "." + PermissionActions.Update;
    }

    public static class Folders
    {
        public const string Resource = "folders";

        public const string Read = Module + "." + Resource + "." + PermissionActions.Read;
        public const string Create = Module + "." + Resource + "." + PermissionActions.Create;
        public const string Update = Module + "." + Resource + "." + PermissionActions.Update;
        public const string Delete = Module + "." + Resource + "." + PermissionActions.Delete;
    }

    public static class Media
    {
        public const string Resource = "media";

        public const string Read = Module + "." + Resource + "." + PermissionActions.Read;
        public const string Create = Module + "." + Resource + "." + PermissionActions.Create;
        public const string Update = Module + "." + Resource + "." + PermissionActions.Update;
        public const string Delete = Module + "." + Resource + "." + PermissionActions.Delete;
    }

    public static class Tags
    {
        public const string Resource = "tags";

        public const string Read = Module + "." + Resource + "." + PermissionActions.Read;
        public const string Update = Module + "." + Resource + "." + PermissionActions.Update;
    }
}
