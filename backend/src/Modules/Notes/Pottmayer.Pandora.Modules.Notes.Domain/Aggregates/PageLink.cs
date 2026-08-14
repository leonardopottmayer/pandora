using Pottmayer.Pandora.Modules.Notes.Domain.ValueObjects;
using Pottmayer.Tars.Core.Ddd;

namespace Pottmayer.Pandora.Modules.Notes.Domain.Aggregates;

/// <summary>
/// An edge of the wiki graph (nte004), derived from the source page's markdown rather than authored
/// directly: every save of a page wipes and rewrites the edges whose source it is. Unlike the
/// <see cref="Page.ParentId"/> tree, this graph may contain cycles. Edges are never edited — only
/// created and removed — so the record carries just a <see cref="CreatedAt"/>.
/// </summary>
public sealed class PageLink : AggregateRoot<Guid>
{
    public Guid SourcePageId { get; private set; }
    public Guid TargetPageId { get; private set; }
    public PageLinkKind Kind { get; private set; } = null!;
    public DateTimeOffset CreatedAt { get; private set; }

    private PageLink() { }

    /// <summary>
    /// Records that <paramref name="sourcePageId"/> references <paramref name="targetPageId"/>. Both
    /// pages exist and belong to the same user — the caller resolved the target before this runs.
    /// </summary>
    public static PageLink Create(
        Guid sourcePageId, Guid targetPageId, PageLinkKind kind, TimeProvider timeProvider) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            SourcePageId = sourcePageId,
            TargetPageId = targetPageId,
            Kind = kind,
            CreatedAt = timeProvider.GetUtcNow()
        };
}
