using Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;
using Pottmayer.Tars.Data.Relational.Abstractions.Repositories;

namespace Pottmayer.Pandora.Modules.Finances.Domain.Ports.Repositories;

public interface IImportLayoutRepository : IStandardRepository<ImportLayout, Guid>
{
    Task<ImportLayout?> FindByCodeAsync(string layoutCode, CancellationToken ct = default);
    Task<IReadOnlyList<ImportLayout>> GetSystemLayoutsAsync(CancellationToken ct = default);
}
