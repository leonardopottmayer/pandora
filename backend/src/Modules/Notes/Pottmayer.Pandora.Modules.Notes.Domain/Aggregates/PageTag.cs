using Pottmayer.Tars.Core.Ddd;

namespace Pottmayer.Pandora.Modules.Notes.Domain.Aggregates;

/// <summary>
/// The fact that a page carries a tag (nte006), derived from the page's markdown exactly like
/// <see cref="PageLink"/>: every save recomputes the set from the text. Never edited — created and
/// removed — so the record carries just a <see cref="CreatedAt"/>.
/// </summary>
public sealed class PageTag : AggregateRoot<Guid>
{
    public Guid PageId { get; private set; }
    public Guid TagId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private PageTag() { }

    /// <summary>
    /// Attaches <paramref name="tagId"/> to <paramref name="pageId"/>. Both belong to the same user —
    /// the caller resolved the tag from the page's own content before this runs.
    /// </summary>
    public static PageTag Create(Guid pageId, Guid tagId, TimeProvider timeProvider) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            PageId = pageId,
            TagId = tagId,
            CreatedAt = timeProvider.GetUtcNow()
        };
}
