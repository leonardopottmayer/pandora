namespace Pottmayer.Pandora.Modules.Agenda.Application.Dtos;

/// <summary>
/// A calendar as shown in the sidebar and returned after a change. <see cref="IsReadOnly"/>: fed by a
/// Google calendar Pandora cannot write to, so its events are edited in Google.
/// </summary>
public sealed record CalendarDto(
    Guid Id,
    string Name,
    string? Color,
    bool IsDefault,
    bool IsVisible,
    string TimeZone,
    string Origin,
    DateTimeOffset? ArchivedAt,
    bool IsReadOnly);
