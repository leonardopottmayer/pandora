using Pottmayer.Pandora.Modules.Files.Domain.Entities;
using Pottmayer.Pandora.Modules.Files.Domain.ReadModels;
using Pottmayer.Pandora.Modules.Files.Domain.ValueObjects;
using Pottmayer.Tars.Data.Abstractions.Repositories;

namespace Pottmayer.Pandora.Modules.Files.Domain.Ports.Repositories;

public interface IEntryRepository : IStandardRepository<Entry, Guid>
{
    Task<Entry?> FindForUserAsync(Guid id, Guid userId, CancellationToken ct = default);

    /// <summary>Tracked, for a scan batch.</summary>
    Task<IReadOnlyList<Entry>> ListByPathsAsync(Guid rootId, IReadOnlyCollection<string> paths, CancellationToken ct = default);

    /// <summary>Present entries of the root the scan did not see: the candidates for missing or excluded.</summary>
    Task<IReadOnlyList<UnseenEntry>> ListUnseenAsync(Guid rootId, Guid scanId, CancellationToken ct = default);

    /// <summary>
    /// Unseen files of the root whose fingerprint (and size) matches exactly one file seen since
    /// <paramref name="since"/> — in this scan, or in any other root of the user — and no other unseen file.
    /// Duplicates are left alone: new + missing is the safe answer.
    /// </summary>
    Task<IReadOnlyList<MovePair>> FindMovesAsync(Guid userId, Guid rootId, Guid scanId, DateTimeOffset since, CancellationToken ct = default);

    /// <summary>Each old entry takes its newer twin's place, which is removed.</summary>
    Task ApplyMovesAsync(IReadOnlyList<MovePair> moves, CancellationToken ct = default);

    /// <summary>Sets missing or excluded, from now on, and puts them back in the review inbox.</summary>
    Task MarkAsync(IReadOnlyCollection<Guid> ids, EntryStatus status, DateTimeOffset now, CancellationToken ct = default);

    /// <summary>A removed root: everything present becomes excluded.</summary>
    Task ExcludeRootAsync(Guid rootId, DateTimeOffset now, CancellationToken ct = default);

    Task<int> CountPresentAsync(Guid rootId, DateTimeOffset? firstSeenBefore = null, CancellationToken ct = default);

    /// <summary>The children of one folder, folders first then by name.</summary>
    Task<IReadOnlyList<Entry>> BrowseAsync(Guid rootId, string parentPath, int skip, int take, CancellationToken ct = default);

    Task<IReadOnlyList<Entry>> SearchAsync(EntrySearch search, int skip, int take, CancellationToken ct = default);

    /// <summary>Every entry of the given roots, for testing a filter against the catalog.</summary>
    IAsyncEnumerable<CatalogItem> StreamAsync(IReadOnlyCollection<Guid> rootIds, CancellationToken ct = default);

    /// <summary>Entries waiting for review (missing or excluded, not kept), counted per root.</summary>
    Task<IReadOnlyDictionary<Guid, int>> CountReviewByRootAsync(Guid userId, CancellationToken ct = default);

    /// <summary>One level of the review tree under <paramref name="parentPath"/>, with counts.</summary>
    Task<IReadOnlyList<ReviewNode>> ListReviewChildrenAsync(Guid rootId, string parentPath, int limit, CancellationToken ct = default);

    /// <summary>
    /// The review decision on entries waiting for it, by id or by folder (everything waiting at or below
    /// it): <c>forget</c> deletes them, <c>keep</c> takes them out of the inbox. Returns how many it touched.
    /// </summary>
    Task<int> DecideReviewAsync(
        Guid userId, ReviewDecision decision, IReadOnlyCollection<Guid> entryIds,
        IReadOnlyCollection<(Guid RootId, string Path)> folders, DateTimeOffset now, CancellationToken ct = default);
}
