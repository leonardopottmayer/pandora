using Pottmayer.Tars.Core.Primitives.Outcomes;

namespace Pottmayer.Pandora.Modules.Agenda.Application.Errors;

public static class GoogleCalendarErrors
{
    /// <summary>The account's token was refused or could not be refreshed: the user has to reconnect it.</summary>
    public static Error AuthFailed(string detail) =>
        Error.Validation("Agenda.GoogleAuthFailed", $"The Google account could not be used: {detail}");

    /// <summary>The token was granted without a calendar scope.</summary>
    public static Error MissingScope =>
        Error.Validation("Agenda.GoogleMissingScope", "The Google account was connected without access to its calendars. Reconnect it and allow Google Calendar.");

    /// <summary>Google's 410: the sync token expired, so the next listing has to be a full one.</summary>
    public static Error SyncTokenExpired =>
        Error.Conflict("Agenda.GoogleSyncTokenExpired", "Google asked for a full sync.");

    public static Error CalendarNotFound =>
        Error.NotFound("Agenda.GoogleCalendarNotFound", "This Google calendar does not exist or the account cannot see it any more.");

    public static Error Failed(string detail) =>
        Error.Validation("Agenda.GoogleFailed", detail);

    public static Error BindingNotFound =>
        Error.NotFound("Agenda.CalendarBindingNotFound", "This Google calendar link does not exist.");

    public static Error AccountNotGoogle =>
        Error.Validation("Agenda.GoogleAccountRequired", "Pick a connected Google account.");

    public static Error NothingToBind =>
        Error.Validation("Agenda.GoogleNothingToBind", "Pick at least one Google calendar.");

    /// <summary>Whether the failure needs the user (reconnect), rather than a retry.</summary>
    public static bool NeedsReconnect(Error error) =>
        error.Code is "Agenda.GoogleAuthFailed" or "Agenda.GoogleMissingScope";
}
