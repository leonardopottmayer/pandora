namespace Pottmayer.Pandora.Modules.Identity.Application.Dtos;

public sealed record UserPreferencesDto(
    string Theme,
    string Language,
    string TimeZone,
    string WeekStartsOn,
    int DefaultAlertOffsetMinutes);
