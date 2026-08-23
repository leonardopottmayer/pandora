namespace Pottmayer.Pandora.Modules.Identity.Presentation.Requests;

public sealed record UpsertPreferencesRequest(
    string Theme,
    string Language,
    string TimeZone,
    string WeekStartsOn,
    int DefaultAlertOffsetMinutes);
