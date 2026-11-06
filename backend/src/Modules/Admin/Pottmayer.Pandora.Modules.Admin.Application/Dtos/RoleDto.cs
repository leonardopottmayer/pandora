namespace Pottmayer.Pandora.Modules.Admin.Application.Dtos;

/// <summary>A role with its patterns and how many users hold it.</summary>
public sealed record RoleDto(
    Guid Id,
    string Name,
    string? Description,
    bool IsSystem,
    bool IsDefault,
    IReadOnlyList<string> Patterns,
    int Holders);
