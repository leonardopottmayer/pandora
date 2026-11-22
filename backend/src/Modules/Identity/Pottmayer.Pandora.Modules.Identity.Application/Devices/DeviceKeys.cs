using System.Security.Cryptography;
using System.Text;

namespace Pottmayer.Pandora.Modules.Identity.Application.Devices;

/// <summary>
/// Device keys: opaque, shown once at pairing, stored only as SHA-256. The <c>pdk_</c> prefix makes a
/// leaked key recognizable (in logs, in a secret scanner).
/// </summary>
internal static class DeviceKeys
{
    private const string Prefix = "pdk_";

    public static string Generate() =>
        Prefix + Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    /// <summary>SHA-256 of the key, hex-encoded (64 chars). Deterministic, so it can be looked up.</summary>
    public static string Hash(string key) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key))).ToLowerInvariant();
}
