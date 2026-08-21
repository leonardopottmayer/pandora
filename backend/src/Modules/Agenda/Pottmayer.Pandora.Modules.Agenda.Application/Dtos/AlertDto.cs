namespace Pottmayer.Pandora.Modules.Agenda.Application.Dtos;

/// <summary>An alert as returned after it is created.</summary>
public sealed record AlertDto(
    Guid Id,
    string SubjectType,
    Guid SubjectId,
    int OffsetMinutes,
    IReadOnlyList<string>? Channels,
    bool IsEnabled);
