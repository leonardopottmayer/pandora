using Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;
using Pottmayer.Tars.Data.Abstractions.Repositories;

namespace Pottmayer.Pandora.Modules.Finances.Domain.Ports.Repositories;

public interface IPositionValuationRepository : IStandardRepository<PositionValuation, Guid>
{
    Task<PositionValuation?> FindOnDateAsync(Guid positionId, DateOnly valuedOn, CancellationToken ct = default);

    /// <summary>A position's values, newest first.</summary>
    Task<IReadOnlyList<PositionValuation>> ListByPositionAsync(Guid positionId, CancellationToken ct = default);

    /// <summary>The latest value of each position that has one.</summary>
    Task<IReadOnlyDictionary<Guid, PositionValuation>> GetLatestAsync(IReadOnlyCollection<Guid> positionIds, CancellationToken ct = default);
}
