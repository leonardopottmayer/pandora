namespace Pottmayer.Pandora.Desktop.Abstractions;

/// <summary>Named <c>HttpClient</c>s the shell provides to modules (resolve them from <c>IHttpClientFactory</c>).</summary>
public static class DesktopHttp
{
    /// <summary>
    /// Calls the user's Pandora as this device: relative URIs go to the configured server and every request
    /// carries the device key. Requests fail with 401 while the PC is not paired, or after a revocation.
    /// </summary>
    public const string DeviceClient = "pandora-device";
}
