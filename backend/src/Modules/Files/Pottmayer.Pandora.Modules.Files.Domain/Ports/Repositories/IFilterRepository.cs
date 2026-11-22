using Pottmayer.Pandora.Modules.Files.Domain.Entities;
using Pottmayer.Tars.Data.Abstractions.Repositories;

namespace Pottmayer.Pandora.Modules.Files.Domain.Ports.Repositories;

public interface IFilterRepository : IStandardRepository<Filter, Guid>
{
    Task<IReadOnlyList<Filter>> ListByUserAsync(Guid userId, CancellationToken ct = default);

    Task<Filter?> FindForUserAsync(Guid id, Guid userId, CancellationToken ct = default);

    /// <summary>The enabled filters that reach a root: the user's, its device's, its own and its folders'.</summary>
    Task<IReadOnlyList<Filter>> ListEnabledForRootAsync(Guid userId, Guid deviceId, Guid rootId, CancellationToken ct = default);
}
