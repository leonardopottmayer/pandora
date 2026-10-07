using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Pottmayer.Pandora.Modules.Identity.Abstractions;

namespace Pottmayer.Pandora.Modules.Identity.Infrastructure.Security;

/// <summary>
/// Builds the device policies on demand, so a module asks for a scope by name
/// (<c>device-scope:files.agent</c>) without registering anything. Every other policy name falls
/// through to the default provider.
/// </summary>
internal sealed class DevicePolicyProvider(IOptions<AuthorizationOptions> options)
    : DefaultAuthorizationPolicyProvider(options)
{
    public override Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (policyName == DeviceAuthorization.Policy)
            return Task.FromResult<AuthorizationPolicy?>(DevicePolicy().Build());

        if (policyName.StartsWith(DeviceAuthorization.ScopePolicyPrefix, StringComparison.Ordinal))
        {
            var scope = policyName[DeviceAuthorization.ScopePolicyPrefix.Length..];
            return Task.FromResult<AuthorizationPolicy?>(
                DevicePolicy().RequireClaim(DeviceAuthorization.ScopeClaim, scope).Build());
        }

        return base.GetPolicyAsync(policyName);
    }

    /// <summary>Authenticated by the device scheme only — a session token never satisfies it.</summary>
    private static AuthorizationPolicyBuilder DevicePolicy() =>
        new AuthorizationPolicyBuilder(DeviceAuthorization.Scheme)
            .RequireAuthenticatedUser()
            .RequireClaim(DeviceAuthorization.DeviceIdClaim);
}
