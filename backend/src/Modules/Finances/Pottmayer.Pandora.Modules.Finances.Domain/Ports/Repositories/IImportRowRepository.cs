using Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;
using Pottmayer.Tars.Data.Relational.Abstractions.Repositories;

namespace Pottmayer.Pandora.Modules.Finances.Domain.Ports.Repositories;

public interface IImportRowRepository : IStandardRepository<ImportRow, Guid>
{
    Task<IReadOnlyList<ImportRow>> GetByImportFileAsync(Guid importFileId, CancellationToken ct = default);

    /// <summary>
    /// Returns all rows for the given user+destination that have a matching dedup key, across all
    /// previous imports. Used to detect certain duplicates before creating a suggestion.
    /// </summary>
    Task<IReadOnlyList<ImportRow>> FindByDedupKeyAsync(
        Guid userId, Guid? accountId, Guid? cardId, string dedupKey,
        IImportFileRepository fileRepo, CancellationToken ct = default);

    /// <summary>
    /// Returns all rows for the given user+destination that have a matching external ID (FITID).
    /// </summary>
    Task<IReadOnlyList<ImportRow>> FindByExternalIdAsync(
        Guid userId, Guid? accountId, Guid? cardId, string externalId,
        IImportFileRepository fileRepo, CancellationToken ct = default);
}
