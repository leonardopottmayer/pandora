using Pottmayer.Pandora.Modules.Files.Domain.Entities;
using Pottmayer.Tars.Data.Abstractions.Repositories;

namespace Pottmayer.Pandora.Modules.Files.Domain.Ports.Repositories;

public interface IScanRepository : IStandardRepository<Scan, Guid>
{
    /// <summary>The root's running and held scans.</summary>
    Task<IReadOnlyList<Scan>> ListOpenByRootAsync(Guid rootId, CancellationToken ct = default);

    /// <summary>Newest first.</summary>
    Task<IReadOnlyList<Scan>> ListByRootAsync(Guid rootId, int limit, CancellationToken ct = default);

    /// <summary>Running scans with no batch (or start) since <paramref name="cutoff"/>.</summary>
    Task<IReadOnlyList<Scan>> ListStaleRunningAsync(DateTimeOffset cutoff, CancellationToken ct = default);
}
