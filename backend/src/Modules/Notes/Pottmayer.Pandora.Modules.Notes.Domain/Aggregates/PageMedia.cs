using Pottmayer.Tars.Core.Ddd;

namespace Pottmayer.Pandora.Modules.Notes.Domain.Aggregates;

/// <summary>
/// That a page shows a library item (nte010), derived from the page's markdown like a
/// <see cref="PageLink"/>: every save rewrites the page's rows, and deleting the page drops them.
/// Never edited, so it carries just a <see cref="CreatedAt"/>.
/// </summary>
public sealed class PageMedia : AggregateRoot<Guid>
{
    public Guid PageId { get; private set; }
    public Guid MediaId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private PageMedia() { }

    /// <summary>The caller checked the item is the page owner's before this runs.</summary>
    public static PageMedia Create(Guid pageId, Guid mediaId, TimeProvider timeProvider) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            PageId = pageId,
            MediaId = mediaId,
            CreatedAt = timeProvider.GetUtcNow()
        };
}
