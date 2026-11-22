using Pottmayer.Tars.Security.Authorization.Abstractions;

namespace Pottmayer.Pandora.Modules.Agenda.Abstractions;

/// <summary>
/// Permission keys of the Agenda module (<c>agenda.resource.action</c>). The module declares them in its
/// <c>ApplicationDI</c>; requests, Assistant tools and checks reference these constants instead of strings.
/// </summary>
public static class AgendaPermissions
{
    /// <summary>Module segment of every key.</summary>
    public const string Module = AgendaModule.Name;

    public static class Calendars
    {
        public const string Resource = "calendars";

        public const string Read = Module + "." + Resource + "." + PermissionActions.Read;
        public const string Create = Module + "." + Resource + "." + PermissionActions.Create;
        public const string Update = Module + "." + Resource + "." + PermissionActions.Update;
        public const string Delete = Module + "." + Resource + "." + PermissionActions.Delete;
    }

    public static class Events
    {
        public const string Resource = "events";

        public const string Read = Module + "." + Resource + "." + PermissionActions.Read;
        public const string Create = Module + "." + Resource + "." + PermissionActions.Create;
        public const string Update = Module + "." + Resource + "." + PermissionActions.Update;
        public const string Delete = Module + "." + Resource + "." + PermissionActions.Delete;
    }

    public static class Reminders
    {
        public const string Resource = "reminders";

        public const string Read = Module + "." + Resource + "." + PermissionActions.Read;
        public const string Create = Module + "." + Resource + "." + PermissionActions.Create;
        public const string Update = Module + "." + Resource + "." + PermissionActions.Update;
        public const string Delete = Module + "." + Resource + "." + PermissionActions.Delete;
    }

    public static class Lists
    {
        public const string Resource = "lists";

        public const string Read = Module + "." + Resource + "." + PermissionActions.Read;
        public const string Create = Module + "." + Resource + "." + PermissionActions.Create;
        public const string Update = Module + "." + Resource + "." + PermissionActions.Update;
        public const string Delete = Module + "." + Resource + "." + PermissionActions.Delete;
    }

    public static class Tasks
    {
        public const string Resource = "tasks";

        public const string Read = Module + "." + Resource + "." + PermissionActions.Read;
        public const string Create = Module + "." + Resource + "." + PermissionActions.Create;
        public const string Update = Module + "." + Resource + "." + PermissionActions.Update;
        public const string Delete = Module + "." + Resource + "." + PermissionActions.Delete;
    }

    public static class Alerts
    {
        public const string Resource = "alerts";

        public const string Read = Module + "." + Resource + "." + PermissionActions.Read;
        public const string Create = Module + "." + Resource + "." + PermissionActions.Create;
        public const string Delete = Module + "." + Resource + "." + PermissionActions.Delete;
    }

    public static class Preferences
    {
        public const string Resource = "preferences";

        public const string Read = Module + "." + Resource + "." + PermissionActions.Read;
        public const string Update = Module + "." + Resource + "." + PermissionActions.Update;
    }
}
