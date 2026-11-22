using Pottmayer.Tars.Core.Ddd;

namespace Pottmayer.Pandora.Modules.Notes.Domain.Aggregates;

/// <summary>
/// A comment on a page (nte013), hung on one block. A thread is its first comment and the replies
/// that point at it (<see cref="ThreadId"/>); only the first carries the <see cref="Quote"/> of the
/// text it is about and the <see cref="ResolvedAt"/> of the thread.
/// </summary>
public sealed class PageComment : AggregateRoot<Guid>
{
    public const int MaxBody = 5000;
    public const int MaxQuote = 500;
    public const int MaxBlockId = 64;

    public Guid PageId { get; private set; }
    public Guid UserId { get; private set; }
    public string BlockId { get; private set; } = string.Empty;
    /// <summary>The first comment of the thread, or null when this is the first.</summary>
    public Guid? ThreadId { get; private set; }
    /// <summary>The words the thread is about, when it is on a part of the text and not the whole block.</summary>
    public string? Quote { get; private set; }
    public string Body { get; private set; } = string.Empty;
    public DateTimeOffset? ResolvedAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? EditedAt { get; private set; }

    public bool IsThread => ThreadId is null;

    private PageComment() { }

    /// <summary>The caller checked the page is the user's and the texts fit.</summary>
    public static PageComment Start(Guid pageId, Guid userId, string blockId, string? quote, string body, TimeProvider timeProvider) => new()
    {
        Id = Guid.CreateVersion7(),
        PageId = pageId,
        UserId = userId,
        BlockId = blockId,
        Quote = string.IsNullOrWhiteSpace(quote) ? null : quote.Trim(),
        Body = body.Trim(),
        CreatedAt = timeProvider.GetUtcNow()
    };

    public PageComment Reply(string body, TimeProvider timeProvider) => new()
    {
        Id = Guid.CreateVersion7(),
        PageId = PageId,
        UserId = UserId,
        BlockId = BlockId,
        ThreadId = Id,
        Body = body.Trim(),
        CreatedAt = timeProvider.GetUtcNow()
    };

    public void Edit(string body, TimeProvider timeProvider)
    {
        Body = body.Trim();
        EditedAt = timeProvider.GetUtcNow();
    }

    public void SetResolved(bool resolved, TimeProvider timeProvider) =>
        ResolvedAt = resolved ? ResolvedAt ?? timeProvider.GetUtcNow() : null;
}
