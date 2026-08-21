namespace Pottmayer.Pandora.Modules.Agenda.Application.Dtos;

/// <summary>A task list as shown in the sidebar and returned after a change.</summary>
public sealed record TaskListDto(
    Guid Id,
    string Name,
    bool IsDefault,
    int Position,
    DateTimeOffset? ArchivedAt);
