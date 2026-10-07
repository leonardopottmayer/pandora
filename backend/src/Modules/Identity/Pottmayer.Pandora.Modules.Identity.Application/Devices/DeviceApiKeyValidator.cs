using Pottmayer.Pandora.Modules.Identity.Abstractions;
using Pottmayer.Pandora.Modules.Identity.Domain.Ports.Repositories;
using Pottmayer.Pandora.Shared.Domain;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;
using Pottmayer.Tars.Security.Identity.Abstractions.Contracts;
using Pottmayer.Tars.Security.Identity.Abstractions.Results;

namespace Pottmayer.Pandora.Modules.Identity.Application.Devices;

/// <summary>
/// Turns an <c>X-Api-Key</c> into the device's principal: the user's <c>Id</c> (so the user context works
/// exactly as with a session), the device id and one <c>scope</c> claim per granted scope. A revoked or
/// unknown key fails authentication.
/// </summary>
public sealed class DeviceApiKeyValidator(IUnitOfWorkFactory factory, TimeProvider timeProvider) : IApiKeyValidator
{
    public async ValueTask<AuthenticationResult?> ValidateAsync(string apiKey, CancellationToken cancellationToken = default)
    {
        var keyHash = DeviceKeys.Hash(apiKey);

        var device = await factory.ExecuteAsync(IdentityModule.DatabaseKey, async (ctx, ct) =>
        {
            var devices = ctx.AcquireRepository<IDeviceRepository>();
            var found = await devices.FindActiveByKeyHashAsync(keyHash, ct);
            if (found is not null && found.Touch(timeProvider.GetUtcNow()))
                await devices.UpdateAsync(found, ct);
            return found;
        }, cancellationToken: cancellationToken);

        if (device is null) return null;

        var userId = device.UserId.ToString();
        return new AuthenticationResult
        {
            Subject = userId,
            Claims =
            [
                new ClaimData(nameof(UserData.Id), userId),
                new ClaimData(DeviceAuthorization.DeviceIdClaim, device.Id.ToString()),
                .. device.Scopes.Select(s => new ClaimData(DeviceAuthorization.ScopeClaim, s)),
            ],
        };
    }
}
