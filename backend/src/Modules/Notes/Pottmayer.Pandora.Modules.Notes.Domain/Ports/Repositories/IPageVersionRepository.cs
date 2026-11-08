using Pottmayer.Pandora.Modules.Notes.Domain.Aggregates;
using Pottmayer.Tars.Data.Abstractions.Repositories;

namespace Pottmayer.Pandora.Modules.Notes.Domain.Ports.Repositories;

public interface IPageVersionRepository : IStandardRepository<PageVersion, Guid>
{
    Task<IReadOnlyList<PageVersionInfo>> ListAsync(Guid pageId, Guid userId, long? before, CancellationToken ct);
    Task<PageVersion?> FindAsync(Guid pageId, Guid userId, long number, CancellationToken ct);
    /// <summary>Drops a page's whole history, straight in the store, when the page is deleted for good.</summary>
    Task PurgeByPageAsync(Guid pageId, CancellationToken ct);
}

public sealed record PageVersionInfo(long Number, DateTimeOffset CreatedAt, string Source, long? RestoredFrom);
