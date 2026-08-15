using Pottmayer.Pandora.Modules.Notes.Domain.Aggregates;

namespace Pottmayer.Pandora.Modules.Notes.Application.Dtos;

/// <summary>Full view of a page, including its markdown body — returned when opening one page.</summary>
public sealed record PageDto(
    Guid Id,
    Guid? ParentId,
    string Title,
    string Slug,
    string ContentMarkdown,
    string? Icon,
    int OrderIndex,
    bool IsFavorite,
    bool IsArchived,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    IReadOnlyList<PageTagDto> Tags)
{
    /// <summary>
    /// The page with the tags its markdown carries — resolved by the caller, which has just written
    /// them (a save) or read them alongside the page.
    /// </summary>
    public static PageDto From(Page p, IEnumerable<Tag> tags) =>
        new(p.Id, p.ParentId, p.Title, p.Slug, p.ContentMarkdown, p.Icon, p.OrderIndex,
            p.IsFavorite, p.IsArchived, p.CreatedAt, p.UpdatedAt,
            [.. tags.OrderBy(t => t.Name).Select(PageTagDto.From)]);
}
