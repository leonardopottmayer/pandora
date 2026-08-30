using Pottmayer.Pandora.Modules.Identity.Abstractions;
using Pottmayer.Pandora.Modules.Identity.Abstractions.Models;
using Pottmayer.Pandora.Modules.Identity.Abstractions.Ports;
using Pottmayer.Pandora.Modules.Identity.Domain.Ports.Repositories;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Identity.Application.Preferences;

/// <summary>Read-only preference snapshots for other modules. No aggregate crosses this boundary.</summary>
public sealed class UserPreferencesReader(IUnitOfWorkFactory factory) : IUserPreferencesReader
{
    public async Task<UserPreferencesSnapshot?> GetAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await factory.ExecuteAsync(IdentityModule.DatabaseKey, async (ctx, token) =>
        {
            var repo = ctx.AcquireRepository<IUserRepository>();
            return await repo.FindByIdWithPreferencesAsync(userId, token);
        }, cancellationToken: ct);

        if (user?.Preferences is null)
            return null;

        var prefs = user.Preferences;
        return new UserPreferencesSnapshot(prefs.TimeZone, prefs.WeekStartsOn, prefs.DefaultAlertOffsetMinutes);
    }
}
