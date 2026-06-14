namespace Pottmayer.Pandora.Modules.Finances.Application.Dtos;

public sealed record SystemCategoryDto(
    Guid Id,
    string Code,
    string Name,
    string Nature,
    string? Color,
    string? Icon,
    int DisplayOrder,
    bool IsOther,
    bool IsActive,
    IReadOnlyList<SystemCategoryDto> Children);
