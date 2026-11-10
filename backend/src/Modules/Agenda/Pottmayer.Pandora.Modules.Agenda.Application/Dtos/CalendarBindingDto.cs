namespace Pottmayer.Pandora.Modules.Agenda.Application.Dtos;

/// <summary>A Pandora calendar fed by a Google calendar, as the settings screen lists it.</summary>
public sealed record CalendarBindingDto(
    Guid Id,
    Guid CalendarId,
    string CalendarName,
    string? CalendarColor,
    Guid ExternalAccountId,
    string RemoteCalendarId,
    string Status,
    DateTimeOffset? LastSyncedAt,
    string? LastError);

/// <summary>A calendar the Google account can see, and the Pandora calendar it is bound to, if any.</summary>
public sealed record GoogleCalendarDto(
    string Id,
    string Name,
    string? Color,
    bool IsPrimary,
    string? AccessRole,
    Guid? BindingId);
