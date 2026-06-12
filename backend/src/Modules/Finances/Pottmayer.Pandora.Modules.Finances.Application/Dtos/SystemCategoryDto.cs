using Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;

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
    IReadOnlyList<SystemCategoryDto> Children)
{
    public static SystemCategoryDto From(SystemCategory c, IReadOnlyList<SystemCategoryDto> children) =>
        new(c.Id, c.Code, c.Name, c.Nature.Value, c.Color, c.Icon, c.DisplayOrder, c.IsOther, c.IsActive, children);
}
