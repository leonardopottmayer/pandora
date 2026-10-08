using Microsoft.EntityFrameworkCore;
using Pottmayer.Pandora.Modules.Files.Agent;
using Pottmayer.Pandora.Modules.Files.Domain.Entities;
using Pottmayer.Pandora.Modules.Files.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Files.Domain.ReadModels;
using Pottmayer.Pandora.Modules.Files.Domain.ValueObjects;
using Pottmayer.Tars.Data.Abstractions.DataContext;
using Pottmayer.Tars.Data.Relational.Repositories;

namespace Pottmayer.Pandora.Modules.Files.Persistence.Repositories;

public sealed class EntryRepository(IDataContextAccessor accessor)
    : StandardRepository<Entry, Guid>(accessor), IEntryRepository
{
    /// <summary>Keeps each statement's id array a reasonable size when a whole disk goes missing.</summary>
    private const int IdChunk = 10_000;

    public Task<Entry?> FindForUserAsync(Guid id, Guid userId, CancellationToken ct = default) =>
        Set.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id && e.UserId == userId, ct);

    public async Task<IReadOnlyList<Entry>> ListByPathsAsync(Guid rootId, IReadOnlyCollection<string> paths, CancellationToken ct = default) =>
        await Set.Where(e => e.RootId == rootId && paths.Contains(e.RelativePath)).ToListAsync(ct);

    public async Task<IReadOnlyList<UnseenEntry>> ListUnseenAsync(Guid rootId, Guid scanId, CancellationToken ct = default) =>
        await Set.AsNoTracking()
                 .Where(e => e.RootId == rootId && e.Status == EntryStatus.Present && e.LastSeenScanId != scanId)
                 .Select(e => new UnseenEntry(e.Id, e.RelativePath, e.Kind))
                 .ToListAsync(ct);

    public async Task<IReadOnlyList<MovePair>> FindMovesAsync(
        Guid userId, Guid rootId, Guid scanId, DateTimeOffset since, CancellationToken ct = default) =>
        await DbContext.Database.SqlQuery<MovePair>($"""
            WITH unseen AS (
                SELECT id, fingerprint, size_bytes FROM files.fil002_entry
                WHERE root_id = {rootId} AND status = 'present' AND kind = 'file' AND fingerprint IS NOT NULL
                  AND last_seen_scan_id IS DISTINCT FROM {scanId}
            ), fresh AS (
                SELECT id, fingerprint, size_bytes FROM files.fil002_entry
                WHERE user_id = {userId} AND status = 'present' AND kind = 'file' AND fingerprint IS NOT NULL
                  AND first_seen_at > {since}
                  AND (root_id <> {rootId} OR last_seen_scan_id = {scanId})
            ), unseen1 AS (
                SELECT fingerprint, size_bytes, min(id::text)::uuid AS id FROM unseen
                GROUP BY fingerprint, size_bytes HAVING count(*) = 1
            ), fresh1 AS (
                SELECT fingerprint, size_bytes, min(id::text)::uuid AS id FROM fresh
                GROUP BY fingerprint, size_bytes HAVING count(*) = 1
            )
            SELECT u.id AS "OldId", f.id AS "NewId"
            FROM unseen1 u JOIN fresh1 f USING (fingerprint, size_bytes)
            """).ToListAsync(ct);

    public async Task ApplyMovesAsync(IReadOnlyList<MovePair> moves, CancellationToken ct = default)
    {
        if (moves.Count == 0) return;

        var ids = moves.SelectMany(m => new[] { m.OldId, m.NewId }).ToList();
        var entries = await Set.Where(e => ids.Contains(e.Id)).ToDictionaryAsync(e => e.Id, ct);

        foreach (var move in moves)
            entries[move.OldId].MoveTo(entries[move.NewId]);

        // The newer twins go now, so the older entries can take their paths when the unit of work saves.
        var newIds = moves.Select(m => m.NewId).ToList();
        await Set.Where(e => newIds.Contains(e.Id)).ExecuteDeleteAsync(ct);
        foreach (var id in newIds)
            DbContext.Entry(entries[id]).State = EntityState.Detached;
    }

    public async Task MarkAsync(IReadOnlyCollection<Guid> ids, EntryStatus status, DateTimeOffset now, CancellationToken ct = default)
    {
        foreach (var chunk in ids.Chunk(IdChunk))
            await Set.Where(e => chunk.Contains(e.Id))
                     .ExecuteUpdateAsync(s => s
                         .SetProperty(e => e.Status, status)
                         .SetProperty(e => e.MissingSince, now)
                         .SetProperty(e => e.KeptAt, (DateTimeOffset?)null), ct);
    }

    public Task ExcludeRootAsync(Guid rootId, DateTimeOffset now, CancellationToken ct = default) =>
        Set.Where(e => e.RootId == rootId && e.Status == EntryStatus.Present)
           .ExecuteUpdateAsync(s => s
               .SetProperty(e => e.Status, EntryStatus.Excluded)
               .SetProperty(e => e.MissingSince, now)
               .SetProperty(e => e.KeptAt, (DateTimeOffset?)null), ct);

    public Task<int> CountPresentAsync(Guid rootId, DateTimeOffset? firstSeenBefore = null, CancellationToken ct = default) =>
        Set.CountAsync(e => e.RootId == rootId && e.Status == EntryStatus.Present
                            && (firstSeenBefore == null || e.FirstSeenAt < firstSeenBefore), ct);

    public async Task<IReadOnlyList<Entry>> BrowseAsync(Guid rootId, string parentPath, int skip, int take, CancellationToken ct = default) =>
        await Set.AsNoTracking()
                 .Where(e => e.RootId == rootId && e.ParentPath == parentPath && e.RelativePath != CatalogPath.Root)
                 .OrderBy(e => e.Kind == EntryKind.File)
                 .ThenBy(e => e.Name)
                 .Skip(skip)
                 .Take(take)
                 .ToListAsync(ct);

    public async Task<IReadOnlyList<Entry>> SearchAsync(EntrySearch search, int skip, int take, CancellationToken ct = default)
    {
        var query = Set.AsNoTracking().Where(e => e.UserId == search.UserId);

        // Each term is a fragment of the name; the trigram index serves ILIKE '%…%'.
        foreach (var term in search.Terms)
        {
            var pattern = $"%{EscapeLike(term)}%";
            query = query.Where(e => EF.Functions.ILike(e.Name, pattern, @"\"));
        }

        if (search.RootIds is { } rootIds) query = query.Where(e => rootIds.Contains(e.RootId));
        if (search.Category is { } category) query = query.Where(e => e.Category == category);
        if (search.MinSize is { } min) query = query.Where(e => e.SizeBytes >= min);
        if (search.MaxSize is { } max) query = query.Where(e => e.SizeBytes <= max);
        if (search.ModifiedFrom is { } from) query = query.Where(e => e.ModifiedAt >= from);
        if (search.ModifiedTo is { } to) query = query.Where(e => e.ModifiedAt <= to);
        if (search.Status is { } status) query = query.Where(e => e.Status == status);

        return await query.OrderBy(e => e.Name).ThenBy(e => e.Id).Skip(skip).Take(take).ToListAsync(ct);
    }

    public IAsyncEnumerable<CatalogItem> StreamAsync(IReadOnlyCollection<Guid> rootIds, CancellationToken ct = default) =>
        Set.AsNoTracking()
           .Where(e => rootIds.Contains(e.RootId))
           .Select(e => new CatalogItem(e.Id, e.RootId, e.RelativePath, e.Kind))
           .AsAsyncEnumerable();

    public async Task<IReadOnlyDictionary<Guid, int>> CountReviewByRootAsync(Guid userId, CancellationToken ct = default) =>
        await Set.Where(e => e.UserId == userId && e.Status != EntryStatus.Present && e.KeptAt == null)
                 .GroupBy(e => e.RootId)
                 .Select(g => new { RootId = g.Key, Count = g.Count() })
                 .ToDictionaryAsync(x => x.RootId, x => x.Count, ct);

    public async Task<IReadOnlyList<ReviewNode>> ListReviewChildrenAsync(
        Guid rootId, string parentPath, int limit, CancellationToken ct = default)
    {
        var prefix = CatalogPath.ChildPrefix(parentPath);

        // Groups everything waiting below the folder by its next path segment. A child that is itself
        // waiting (no further "/" in what follows the prefix) brings its id, status and kind along.
        return await DbContext.Database.SqlQuery<ReviewNode>($"""
            SELECT name AS "Name",
                   {prefix} || name AS "Path",
                   count(*)::int AS "Count",
                   bool_or(strpos(rest, '/') > 0) AS "HasChildren",
                   max(CASE WHEN strpos(rest, '/') = 0 THEN id::text END)::uuid AS "EntryId",
                   max(CASE WHEN strpos(rest, '/') = 0 THEN status END) AS "Status",
                   max(CASE WHEN strpos(rest, '/') = 0 THEN kind END) AS "Kind"
            FROM (
                SELECT id, status, kind,
                       substr(relative_path, char_length({prefix}) + 1) AS rest,
                       split_part(substr(relative_path, char_length({prefix}) + 1), '/', 1) AS name
                FROM files.fil002_entry
                WHERE root_id = {rootId} AND status <> 'present' AND kept_at IS NULL
                  AND starts_with(relative_path, {prefix}) AND relative_path <> {prefix}
            ) waiting
            GROUP BY name
            ORDER BY bool_or(strpos(rest, '/') > 0) DESC, name
            LIMIT {limit}
            """).ToListAsync(ct);
    }

    public async Task<int> DecideReviewAsync(
        Guid userId, ReviewDecision decision, IReadOnlyCollection<Guid> entryIds,
        IReadOnlyCollection<(Guid RootId, string Path)> folders, DateTimeOffset now, CancellationToken ct = default)
    {
        var waiting = Set.Where(e => e.UserId == userId && e.Status != EntryStatus.Present && e.KeptAt == null);
        var touched = 0;

        if (entryIds.Count > 0)
            touched += await DecideAsync(waiting.Where(e => entryIds.Contains(e.Id)));

        foreach (var (rootId, path) in folders)
        {
            var prefix = CatalogPath.ChildPrefix(path);
            touched += await DecideAsync(waiting.Where(e =>
                e.RootId == rootId && (e.RelativePath == path || e.RelativePath.StartsWith(prefix))));
        }

        return touched;

        Task<int> DecideAsync(IQueryable<Entry> entries) => decision == ReviewDecision.Forget
            ? entries.ExecuteDeleteAsync(ct)
            : entries.ExecuteUpdateAsync(s => s.SetProperty(e => e.KeptAt, now), ct);
    }

    private static string EscapeLike(string value) =>
        value.Replace(@"\", @"\\").Replace("%", @"\%").Replace("_", @"\_");
}
