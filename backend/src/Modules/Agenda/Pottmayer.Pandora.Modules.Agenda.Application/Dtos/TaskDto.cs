namespace Pottmayer.Pandora.Modules.Agenda.Application.Dtos;

/// <summary>A task as shown on the list and returned after a change.</summary>
public sealed record TaskDto(
    Guid Id,
    Guid ListId,
    Guid? ParentTaskId,
    string Title,
    string? Notes,
    DateTimeOffset? DueAt,
    bool DueHasTime,
    string Priority,
    string Status,
    DateTimeOffset? CompletedAt,
    string TimeZone,
    string? Rrule,
    int Position);
