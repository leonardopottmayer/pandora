using Pottmayer.Pandora.Modules.Notes.Domain.Aggregates;

namespace Pottmayer.Pandora.Modules.Notes.Application.Dtos;

/// <summary>
/// Lightweight node for the sidebar tree — no markdown body. The frontend nests these by
/// <see cref="ParentId"/>; ordering within a parent follows <see cref="OrderIndex"/>.
/// </summary>
public sealed record PageSummaryDto(
    Guid Id,
    Guid? ParentId,
    string Title,
    string Slug,
    string? Icon,
    int OrderIndex,
    bool IsFavorite,
    bool IsArchived)
{
    public static PageSummaryDto From(Page p) =>
        new(p.Id, p.ParentId, p.Title, p.Slug, p.Icon, p.OrderIndex, p.IsFavorite, p.IsArchived);
}
