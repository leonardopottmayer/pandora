using System.Text.Json.Nodes;
using Pottmayer.Pandora.Modules.Notes.Domain.Aggregates;

namespace Pottmayer.Pandora.Modules.Notes.Domain.ValueObjects;

/// <summary>The editable state of a page; derived tags, links and media usage are rebuilt on restore.</summary>
public sealed record PageSnapshot(
    string Title,
    string Content,
    string? Icon,
    Guid SpaceId,
    Guid? ParentId,
    Guid? FolderId,
    int OrderIndex,
    bool IsFavorite,
    DateTimeOffset? ArchivedAt,
    DateTimeOffset? DeletedAt)
{
    public static PageSnapshot From(Page page) =>
        new(page.Title, page.Content, page.Icon, page.SpaceId, page.ParentId, page.FolderId,
            page.OrderIndex, page.IsFavorite, page.ArchivedAt, page.DeletedAt);

    /// <summary>
    /// Whether both describe the same page. The blocks compare as JSON, not as text: Postgres hands
    /// <c>jsonb</c> back with its own key order and spacing, so the same document reads differently.
    /// </summary>
    public bool SameStateAs(PageSnapshot other) =>
        this with { Content = string.Empty } == other with { Content = string.Empty } &&
        (Content == other.Content || JsonNode.DeepEquals(JsonNode.Parse(Content), JsonNode.Parse(other.Content)));
}
