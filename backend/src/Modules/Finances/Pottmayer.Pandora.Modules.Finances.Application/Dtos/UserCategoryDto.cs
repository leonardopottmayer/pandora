using Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;

namespace Pottmayer.Pandora.Modules.Finances.Application.Dtos;

public sealed record UserCategoryDto(
    Guid Id,
    string Name,
    string Nature,
    Guid? ParentCategoryId,
    string? Color,
    string? Icon,
    int DisplayOrder,
    bool IsActive,
    IReadOnlyList<UserCategoryDto> Children)
{
    public static UserCategoryDto From(UserCategory c, IReadOnlyList<UserCategoryDto> children) =>
        new(c.Id, c.Name, c.Nature.Value, c.ParentCategoryId, c.Color, c.Icon, c.DisplayOrder, c.IsActive, children);

    public static UserCategoryDto From(UserCategory c) => From(c, []);
}
