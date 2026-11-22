using Pottmayer.Pandora.Modules.Notes.Domain.Aggregates;
using Pottmayer.Tars.Data.Abstractions.Repositories;

namespace Pottmayer.Pandora.Modules.Notes.Domain.Ports.Repositories;

public interface IPageCommentRepository : IStandardRepository<PageComment, Guid>
{
    /// <summary>All the comments of one of the user's pages, oldest first.</summary>
    Task<IReadOnlyList<PageComment>> GetByPageAsync(Guid pageId, Guid userId, CancellationToken ct);
    Task<PageComment?> FindForUserAsync(Guid id, Guid userId, CancellationToken ct);
    /// <summary>Drops a thread's replies, straight in the store, before the thread goes.</summary>
    Task DeleteRepliesAsync(Guid threadId, CancellationToken ct);
    Task PurgeByPageAsync(Guid pageId, CancellationToken ct);
}

public interface IBlockStampRepository : IStandardRepository<BlockStamp, Guid>
{
    /// <summary>The stamps of one page — the set a save reconciles.</summary>
    Task<IReadOnlyList<BlockStamp>> GetByPageAsync(Guid pageId, CancellationToken ct);
    Task PurgeByPageAsync(Guid pageId, CancellationToken ct);
}
