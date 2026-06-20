using Microsoft.EntityFrameworkCore;
using Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Finances.Domain.Ports.Repositories;
using Pottmayer.Tars.Data.Abstractions.DataContext;
using Pottmayer.Tars.Data.Relational.Repositories;

namespace Pottmayer.Pandora.Modules.Finances.Persistence.Repositories;

public sealed class ImportFileRepository(IDataContextAccessor accessor)
    : StandardRepository<ImportFile, Guid>(accessor), IImportFileRepository
{
    public Task<ImportFile?> FindByIdForUserAsync(Guid id, Guid userId, CancellationToken ct = default)
        => Queryable().FirstOrDefaultAsync(f => f.Id == id && f.UserId == userId, ct);

    public Task<ImportFile?> ClaimNextReceivedAsync(CancellationToken ct = default)
        => Queryable()
            .Where(f => f.Status == "received")
            .OrderBy(f => f.CreatedAt)
            .FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<ImportFile>> QueryAsync(
        Guid userId, ImportFileFilter filter, CancellationToken ct = default)
        => await Queryable()
            .Where(f => f.UserId == userId)
            .OrderByDescending(f => f.CreatedAt)
            .Skip(filter.Skip)
            .Take(filter.Take)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<ImportFile>> GetByHashForUserAsync(
        Guid userId, string fileHash, CancellationToken ct = default)
        => await Queryable()
            .Where(f => f.UserId == userId && f.FileHash == fileHash)
            .OrderByDescending(f => f.CreatedAt)
            .ToListAsync(ct);
}
