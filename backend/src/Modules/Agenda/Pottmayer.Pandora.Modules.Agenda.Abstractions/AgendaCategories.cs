namespace Pottmayer.Pandora.Modules.Agenda.Abstractions;

/// <summary>
/// The notification categories Agenda raises. Channels resolves each to the user's channels; the
/// per-channel template variants live in Channels.
/// </summary>
public static class AgendaCategories
{
    /// <summary>The category reminders raise.</summary>
    public const string Reminder = "agenda.reminder";

    /// <summary>The category task alerts raise.</summary>
    public const string Task = "agenda.task";

    /// <summary>The category event alerts raise.</summary>
    public const string Event = "agenda.event";
}
