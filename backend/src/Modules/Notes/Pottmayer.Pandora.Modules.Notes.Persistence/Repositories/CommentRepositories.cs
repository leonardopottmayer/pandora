using Microsoft.EntityFrameworkCore;
using Pottmayer.Pandora.Modules.Notes.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Notes.Domain.Ports.Repositories;
using Pottmayer.Tars.Data.Abstractions.DataContext;
using Pottmayer.Tars.Data.Relational.Repositories;

namespace Pottmayer.Pandora.Modules.Notes.Persistence.Repositories;

public sealed class PageCommentRepository(IDataContextAccessor accessor)
    : StandardRepository<PageComment, Guid>(accessor), IPageCommentRepository
{
    public async Task<IReadOnlyList<PageComment>> GetByPageAsync(Guid pageId, Guid userId, CancellationToken ct)
        => await Queryable().AsNoTracking()
            .Where(c => c.PageId == pageId && c.UserId == userId)
            .OrderBy(c => c.CreatedAt).ThenBy(c => c.Id).ToListAsync(ct);

    public Task<PageComment?> FindForUserAsync(Guid id, Guid userId, CancellationToken ct)
        => Queryable().FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId, ct);

    public Task DeleteRepliesAsync(Guid threadId, CancellationToken ct)
        => Queryable().Where(c => c.ThreadId == threadId).ExecuteDeleteAsync(ct);

    // Replies first: they point at their thread.
    public async Task PurgeByPageAsync(Guid pageId, CancellationToken ct)
    {
        await Queryable().Where(c => c.PageId == pageId && c.ThreadId != null).ExecuteDeleteAsync(ct);
        await Queryable().Where(c => c.PageId == pageId).ExecuteDeleteAsync(ct);
    }
}

public sealed class BlockStampRepository(IDataContextAccessor accessor)
    : StandardRepository<BlockStamp, Guid>(accessor), IBlockStampRepository
{
    public async Task<IReadOnlyList<BlockStamp>> GetByPageAsync(Guid pageId, CancellationToken ct)
        => await Queryable().Where(s => s.PageId == pageId).ToListAsync(ct);

    public Task PurgeByPageAsync(Guid pageId, CancellationToken ct)
        => Queryable().Where(s => s.PageId == pageId).ExecuteDeleteAsync(ct);
}
