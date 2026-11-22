using Pottmayer.Pandora.Modules.Admin.Abstractions;
using Pottmayer.Pandora.Modules.Admin.Domain.Ports.Repositories;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;
using Pottmayer.Tars.FeatureFlags.Abstractions;

namespace Pottmayer.Pandora.Modules.Admin.Application.Authorization;

/// <summary>The flags an administrator overrode (<c>adm001</c>), for the Tars flag service to cache.</summary>
public sealed class FeatureFlagStore(IUnitOfWorkFactory factory) : IFeatureFlagStore
{
    public async Task<IReadOnlyDictionary<string, bool>> GetOverridesAsync(CancellationToken cancellationToken = default)
    {
        var overrides = await factory.ExecuteAsync(AdminModule.DatabaseKey, (ctx, ct) =>
            ctx.AcquireRepository<IFeatureFlagOverrideRepository>().ListAsync(ct),
            cancellationToken: cancellationToken);

        return overrides.ToDictionary(o => o.Key, o => o.Enabled);
    }
}
