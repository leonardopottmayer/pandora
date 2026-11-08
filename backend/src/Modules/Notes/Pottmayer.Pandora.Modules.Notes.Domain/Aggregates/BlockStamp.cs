using Pottmayer.Tars.Core.Ddd;

namespace Pottmayer.Pandora.Modules.Notes.Domain.Aggregates;

/// <summary>
/// When a block of a page was written and last changed, and by what (nte014). Derived on every save
/// by comparing each block's fingerprint with the one stored: a new id is a new block, a different
/// fingerprint an edit, a missing id a removed block.
/// </summary>
public sealed class BlockStamp : AggregateRoot<Guid>
{
    public Guid PageId { get; private set; }
    public string BlockId { get; private set; } = string.Empty;
    public string Fingerprint { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    /// <summary>What wrote the last change, a <c>PageVersionSource</c>: the user's edit, the Assistant, a restore.</summary>
    public string UpdatedSource { get; private set; } = string.Empty;

    private BlockStamp() { }

    public static BlockStamp Create(Guid pageId, string blockId, string fingerprint, string source, TimeProvider timeProvider)
    {
        var now = timeProvider.GetUtcNow();
        return new()
        {
            Id = Guid.CreateVersion7(),
            PageId = pageId,
            BlockId = blockId,
            Fingerprint = fingerprint,
            CreatedAt = now,
            UpdatedAt = now,
            UpdatedSource = source
        };
    }

    /// <returns>Whether the block changed.</returns>
    public bool Touch(string fingerprint, string source, TimeProvider timeProvider)
    {
        if (fingerprint == Fingerprint)
            return false;
        Fingerprint = fingerprint;
        UpdatedAt = timeProvider.GetUtcNow();
        UpdatedSource = source;
        return true;
    }
}
