using Pottmayer.Pandora.Modules.Identity.Abstractions;
using Pottmayer.Pandora.Modules.Identity.Abstractions.Ports;
using Pottmayer.Pandora.Modules.Identity.Domain.Ports.Repositories;
using Pottmayer.Pandora.Shared.Domain.ValueObjects;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Identity.Application.Users;

/// <summary>Finds accounts for other modules. No aggregate crosses this boundary.</summary>
public sealed class UserDirectory(IUnitOfWorkFactory factory) : IUserDirectory
{
    public async Task<Guid?> FindActivatedUserIdByEmailAsync(string email, CancellationToken ct = default)
    {
        if (!Email.TryCreate(email, out var address))
            return null;

        var user = await factory.ExecuteAsync(IdentityModule.DatabaseKey, async (ctx, token) =>
        {
            var repo = ctx.AcquireRepository<IUserRepository>();
            return await repo.FindByEmailAsync(address!, token);
        }, cancellationToken: ct);

        return user?.EmailConfirmedAt is null ? null : user.Id;
    }
}
