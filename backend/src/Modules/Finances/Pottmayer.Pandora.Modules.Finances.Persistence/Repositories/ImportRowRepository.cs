using Microsoft.EntityFrameworkCore;
using Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Finances.Domain.Ports.Repositories;
using Pottmayer.Tars.Data.Abstractions.DataContext;
using Pottmayer.Tars.Data.Relational.Repositories;

namespace Pottmayer.Pandora.Modules.Finances.Persistence.Repositories;

public sealed class ImportRowRepository(IDataContextAccessor accessor)
    : StandardRepository<ImportRow, Guid>(accessor), IImportRowRepository
{
    public async Task<IReadOnlyList<ImportRow>> GetByImportFileAsync(
        Guid importFileId, CancellationToken ct = default)
        => await Queryable()
            .Where(r => r.ImportFileId == importFileId)
            .OrderBy(r => r.RowIndex)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<ImportRow>> FindByDedupKeyAsync(
        Guid userId, Guid? accountId, Guid? cardId, string dedupKey,
        IImportFileRepository fileRepo, CancellationToken ct = default)
    {
        // Get import file IDs for this user+destination first, then filter rows
        var fileIds = await fileRepo.QueryAsync(userId, new ImportFileFilter(0, 1000), ct);
        var matchingFileIds = fileIds
            .Where(f => (accountId == null || f.AccountId == accountId)
                     && (cardId == null || f.CardId == cardId))
            .Select(f => f.Id)
            .ToHashSet();

        if (matchingFileIds.Count == 0) return [];

        return await Queryable()
            .Where(r => matchingFileIds.Contains(r.ImportFileId) && r.DedupKey == dedupKey)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<ImportRow>> FindByExternalIdAsync(
        Guid userId, Guid? accountId, Guid? cardId, string externalId,
        IImportFileRepository fileRepo, CancellationToken ct = default)
    {
        var fileIds = await fileRepo.QueryAsync(userId, new ImportFileFilter(0, 1000), ct);
        var matchingFileIds = fileIds
            .Where(f => (accountId == null || f.AccountId == accountId)
                     && (cardId == null || f.CardId == cardId))
            .Select(f => f.Id)
            .ToHashSet();

        if (matchingFileIds.Count == 0) return [];

        return await Queryable()
            .Where(r => matchingFileIds.Contains(r.ImportFileId) && r.ExternalId == externalId)
            .ToListAsync(ct);
    }
}
