namespace Pottmayer.Pandora.Modules.Agenda.Application.Dtos;

/// <summary>A calendar as shown in the sidebar and returned after a change.</summary>
public sealed record CalendarDto(
    Guid Id,
    string Name,
    string? Color,
    bool IsDefault,
    bool IsVisible,
    string TimeZone,
    string Origin,
    DateTimeOffset? ArchivedAt);
