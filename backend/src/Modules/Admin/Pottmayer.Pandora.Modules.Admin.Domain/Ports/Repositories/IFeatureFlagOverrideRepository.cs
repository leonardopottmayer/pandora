using Pottmayer.Pandora.Modules.Admin.Domain.Entities;
using Pottmayer.Tars.Data.Abstractions.Repositories;

namespace Pottmayer.Pandora.Modules.Admin.Domain.Ports.Repositories;

public interface IFeatureFlagOverrideRepository : IStandardRepository<FeatureFlagOverride, string>
{
    Task<IReadOnlyList<FeatureFlagOverride>> ListAsync(CancellationToken ct = default);
}
