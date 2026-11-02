using Pottmayer.Pandora.Modules.Notes.Domain.Aggregates;

namespace Pottmayer.Pandora.Modules.Notes.Domain.ValueObjects;

/// <summary>The editable state of a page; derived tags, links and media usage are rebuilt on restore.</summary>
public sealed record PageSnapshot(
    string Title,
    string ContentMarkdown,
    string? Icon,
    Guid SpaceId,
    Guid? ParentId,
    int OrderIndex,
    bool IsFavorite,
    DateTimeOffset? ArchivedAt,
    DateTimeOffset? DeletedAt)
{
    public static PageSnapshot From(Page page) =>
        new(page.Title, page.ContentMarkdown, page.Icon, page.SpaceId, page.ParentId,
            page.OrderIndex, page.IsFavorite, page.ArchivedAt, page.DeletedAt);
}
