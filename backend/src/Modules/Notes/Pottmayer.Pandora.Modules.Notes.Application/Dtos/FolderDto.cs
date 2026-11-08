using Pottmayer.Pandora.Modules.Notes.Domain.Aggregates;

namespace Pottmayer.Pandora.Modules.Notes.Application.Dtos;

/// <summary>A folder of a space. The frontend nests them by <see cref="ParentId"/> beside the pages.</summary>
public sealed record FolderDto(
    Guid Id,
    Guid SpaceId,
    Guid? ParentId,
    string Name,
    string? Icon,
    string? Description,
    int OrderIndex,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt)
{
    public static FolderDto From(Folder f) =>
        new(f.Id, f.SpaceId, f.ParentId, f.Name, f.Icon, f.Description, f.OrderIndex, f.CreatedAt, f.UpdatedAt);
}
