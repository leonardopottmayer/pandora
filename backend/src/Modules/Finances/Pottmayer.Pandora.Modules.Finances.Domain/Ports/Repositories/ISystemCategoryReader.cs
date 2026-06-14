using Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;
using Pottmayer.Tars.Data.Abstractions.Repositories;

namespace Pottmayer.Pandora.Modules.Finances.Domain.Ports.Repositories;

/// <summary>Read-only access to the seeded system category tree (fin002).</summary>
public interface ISystemCategoryReader : IRepository
{
    /// <summary>All system categories, optionally filtered by nature and active state.</summary>
    Task<IReadOnlyList<SystemCategory>> GetAllAsync(
        string? nature, bool includeInactive, CancellationToken ct = default);

    /// <summary>Looks up a single seeded category by its stable code (e.g. <c>credit-card-payment</c>).</summary>
    Task<SystemCategory?> GetByCodeAsync(string code, CancellationToken ct = default);
}
