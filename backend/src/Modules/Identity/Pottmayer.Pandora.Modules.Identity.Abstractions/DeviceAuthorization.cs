namespace Pottmayer.Pandora.Modules.Identity.Abstractions;

/// <summary>
/// How an endpoint opts in to device keys (see docs/architecture/en/desktop-client.md §4.5). The default
/// policy stays JWT-only: a device key reaches an endpoint only when the endpoint asks for it, e.g.
/// <code>[Authorize(AuthenticationSchemes = DeviceAuthorization.Scheme, Policy = DeviceAuthorization.ScopePolicyPrefix + "files.agent")]</code>
/// </summary>
public static class DeviceAuthorization
{
    /// <summary>The authentication scheme reading the <c>X-Api-Key</c> header.</summary>
    public const string Scheme = "ApiKey";

    /// <summary>Any paired, non-revoked device.</summary>
    public const string Policy = "device";

    /// <summary>Prefix of the policy requiring one scope: <c>device-scope:files.agent</c>.</summary>
    public const string ScopePolicyPrefix = "device-scope:";

    public const string DeviceIdClaim = "device_id";
    public const string ScopeClaim = "scope";
}
