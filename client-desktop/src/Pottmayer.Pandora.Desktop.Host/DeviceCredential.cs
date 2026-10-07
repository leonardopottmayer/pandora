using System.Diagnostics;
using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Pottmayer.Pandora.Desktop.Abstractions;

namespace Pottmayer.Pandora.Desktop.Host;

/// <summary>The key this PC was paired with (Identity's device, phase D2).</summary>
internal sealed record DeviceCredential(Guid DeviceId, string Key);

/// <summary>
/// Keeps the device key in <c>credentials.bin</c>, encrypted with DPAPI for the current Windows user —
/// another account on the same PC, or a copy of the file elsewhere, cannot read it.
/// </summary>
internal sealed class DeviceCredentialStore(string? dataDir = null)
{
    private readonly string _path = Path.Combine(dataDir ?? DesktopSettingsStore.DataDir, "credentials.bin");

    public DeviceCredential? Load()
    {
        if (!File.Exists(_path)) return null;
        try
        {
            var json = ProtectedData.Unprotect(File.ReadAllBytes(_path), null, DataProtectionScope.CurrentUser);
            return JsonSerializer.Deserialize<DeviceCredential>(json);
        }
        catch (Exception ex) when (ex is CryptographicException or JsonException)
        {
            // Unreadable (another user's file, corruption): the PC is simply not paired.
            return null;
        }
    }

    public void Save(DeviceCredential credential)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        var bytes = ProtectedData.Protect(JsonSerializer.SerializeToUtf8Bytes(credential), null, DataProtectionScope.CurrentUser);
        File.WriteAllBytes(_path, bytes);
    }

    public void Forget() => File.Delete(_path);
}

internal static class DeviceHttpRegistration
{
    /// <summary>
    /// <see cref="DesktopHttp.DeviceClient"/> (base address = the configured server, read on every
    /// <c>CreateClient</c>), and the startup check that forgets a revoked key.
    /// </summary>
    public static IServiceCollection AddDeviceHttpClient(this IServiceCollection services)
    {
        services.AddTransient<DeviceKeyHandler>();
        services.AddHttpClient(DesktopHttp.DeviceClient, (sp, client) =>
                    client.BaseAddress = sp.GetRequiredService<DesktopSettingsStore>().Server)
                .AddHttpMessageHandler<DeviceKeyHandler>();
        services.AddHostedService<DeviceCredentialCheck>();
        return services;
    }
}

/// <summary>Adds the device key to every request; read per request, so a new pairing applies immediately.</summary>
internal sealed class DeviceKeyHandler(DeviceCredentialStore credentials) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (credentials.Load() is { } credential)
            request.Headers.Add("X-Api-Key", credential.Key);

        return base.SendAsync(request, cancellationToken);
    }
}

/// <summary>
/// At startup, asks the server whether this PC's key is still valid. A revoked key (401) is forgotten,
/// so the web shows the PC as not paired. An unreachable server changes nothing.
/// </summary>
internal sealed class DeviceCredentialCheck(IHttpClientFactory http, DeviceCredentialStore credentials) : BackgroundService
{
    public const string MeUri = "api/v1/identity/devices/me";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (credentials.Load() is null) return;
        try
        {
            using var response = await http.CreateClient(DesktopHttp.DeviceClient).GetAsync(MeUri, stoppingToken);
            if (response.StatusCode == HttpStatusCode.Unauthorized) credentials.Forget();
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException)
        {
            Trace.TraceWarning($"Could not verify the device key: {ex.Message}");
        }
    }
}
