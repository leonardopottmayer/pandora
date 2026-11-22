using Pottmayer.Pandora.Modules.Notes.Domain.ValueObjects;
using Pottmayer.Tars.Core.Ddd;

namespace Pottmayer.Pandora.Modules.Notes.Domain.Aggregates;

/// <summary>A complete, immutable page version (nte011).</summary>
public sealed class PageVersion : AggregateRoot<Guid>
{
    public Guid PageId { get; private set; }
    public Guid UserId { get; private set; }
    public long Number { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public string Source { get; private set; } = PageVersionSource.Edit;
    public long? RestoredFrom { get; private set; }
    public PageSnapshot Snapshot { get; private set; } = null!;

    private PageVersion() { }

    public static PageVersion Capture(Page page, long number, TimeProvider timeProvider, string source, long? restoredFrom) => new()
    {
        Id = Guid.CreateVersion7(),
        PageId = page.Id,
        UserId = page.UserId,
        Number = number,
        CreatedAt = timeProvider.GetUtcNow(),
        Source = source,
        RestoredFrom = restoredFrom,
        Snapshot = PageSnapshot.From(page)
    };
}
