namespace Pottmayer.Pandora.Modules.Agenda.Abstractions;

/// <summary>
/// Cross-cutting identity of the Agenda module, shared across its layers.
/// </summary>
public static class AgendaModule
{
    /// <summary>Logical name of the module. Also used as the database routing key.</summary>
    public const string Name = "agenda";

    /// <summary>Database pipeline key (Tars) for this module.</summary>
    public const string DatabaseKey = Name;

    /// <summary>Database schema that owns this module's tables.</summary>
    public const string Schema = Name;

    /// <summary>
    /// The notification category reminders raise, used by Channels to resolve the user's channels.
    /// </summary>
    public const string ReminderCategory = "agenda.reminder";
}
