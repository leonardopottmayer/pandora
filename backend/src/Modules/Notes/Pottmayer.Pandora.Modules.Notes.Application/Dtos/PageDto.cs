using System.Text.Json;
using Pottmayer.Pandora.Modules.Notes.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Notes.Domain.ValueObjects;

namespace Pottmayer.Pandora.Modules.Notes.Application.Dtos;

/// <summary>Full view of a page, including its blocks — returned when opening one page.</summary>
public sealed record PageDto(
    Guid Id,
    Guid SpaceId,
    Guid? ParentId,
    Guid? FolderId,
    string Title,
    string Slug,
    JsonElement Content,
    string? Icon,
    int OrderIndex,
    bool IsFavorite,
    bool IsArchived,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    IReadOnlyList<PageTagDto> Tags,
    PageAppearance Appearance,
    bool IsLocked,
    DateTimeOffset? VerifiedAt,
    DateTimeOffset? VerifiedUntil,
    string? PublicToken,
    bool IsTemplate = false)
{
    /// <summary>
    /// The page with the tags its tags block carries — resolved by the caller, which has just written
    /// them (a save) or read them alongside the page.
    /// </summary>
    public static PageDto From(Page p, IEnumerable<Tag> tags) =>
        new(p.Id, p.SpaceId, p.ParentId, p.FolderId, p.Title, p.Slug, JsonDocument.Parse(p.Content).RootElement.Clone(), p.Icon, p.OrderIndex,
            p.IsFavorite, p.IsArchived, p.CreatedAt, p.UpdatedAt,
            [.. tags.OrderBy(t => t.Name).Select(PageTagDto.From)],
            PageAppearance.Parse(p.Appearance), p.IsLocked, p.VerifiedAt, p.VerifiedUntil, p.PublicToken, p.IsTemplate);
}
