using Microsoft.Extensions.DependencyInjection;
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
/// <remarks>
/// The lookup runs in a scope of its own. Authentication happens before the request has a user, and the
/// request's user context is resolved once and cached: a unit of work committed in the request scope here
/// would let the auditing interceptor cache it empty for the rest of the request.
/// </remarks>
public sealed class DeviceApiKeyValidator(IServiceScopeFactory scopes, TimeProvider timeProvider) : IApiKeyValidator
{
    public async ValueTask<AuthenticationResult?> ValidateAsync(string apiKey, CancellationToken cancellationToken = default)
    {
        var keyHash = DeviceKeys.Hash(apiKey);

        await using var scope = scopes.CreateAsyncScope();
        var factory = scope.ServiceProvider.GetRequiredService<IUnitOfWorkFactory>();
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
                // The user context needs a subject claim to resolve the user, as a session token carries one.
                new ClaimData("sub", userId),
                new ClaimData(nameof(UserData.Id), userId),
                new ClaimData(DeviceAuthorization.DeviceIdClaim, device.Id.ToString()),
                .. device.Scopes.Select(s => new ClaimData(DeviceAuthorization.ScopeClaim, s)),
            ],
        };
    }
}
