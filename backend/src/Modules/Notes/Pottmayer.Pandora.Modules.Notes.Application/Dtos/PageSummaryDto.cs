using Pottmayer.Pandora.Modules.Notes.Domain.Aggregates;

namespace Pottmayer.Pandora.Modules.Notes.Application.Dtos;

/// <summary>
/// Lightweight node for the sidebar tree — no body. The frontend nests these by <see cref="ParentId"/>
/// and <see cref="FolderId"/>; ordering within a parent follows <see cref="OrderIndex"/>.
/// </summary>
public sealed record PageSummaryDto(
    Guid Id,
    Guid SpaceId,
    Guid? ParentId,
    Guid? FolderId,
    string Title,
    string Slug,
    string? Icon,
    int OrderIndex,
    bool IsFavorite,
    bool IsArchived,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    bool IsTemplate = false)
{
    public static PageSummaryDto From(Page p) =>
        new(p.Id, p.SpaceId, p.ParentId, p.FolderId, p.Title, p.Slug, p.Icon, p.OrderIndex, p.IsFavorite, p.IsArchived,
            p.CreatedAt, p.UpdatedAt, p.IsTemplate);
}
