using Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;
using Pottmayer.Tars.Data.Relational.Abstractions.Repositories;

namespace Pottmayer.Pandora.Modules.Finances.Domain.Ports.Repositories;

public interface IImportFileRepository : IStandardRepository<ImportFile, Guid>
{
    Task<ImportFile?> FindByIdForUserAsync(Guid id, Guid userId, CancellationToken ct = default);
    Task<ImportFile?> ClaimNextReceivedAsync(CancellationToken ct = default);
    Task<IReadOnlyList<ImportFile>> QueryAsync(Guid userId, ImportFileFilter filter, CancellationToken ct = default);
    Task<IReadOnlyList<ImportFile>> GetByHashForUserAsync(Guid userId, string fileHash, CancellationToken ct = default);
}

public sealed record ImportFileFilter(int Skip = 0, int Take = 20);
