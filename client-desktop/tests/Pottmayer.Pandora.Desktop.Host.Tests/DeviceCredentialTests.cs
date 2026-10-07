using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Pottmayer.Pandora.Desktop.Abstractions;
using Pottmayer.Pandora.Desktop.Host;
using Xunit;

namespace Pottmayer.Pandora.Desktop.Host.Tests;

public sealed class DeviceCredentialTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "pandora-desktop-tests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    [Fact]
    public void The_key_round_trips_encrypted_and_can_be_forgotten()
    {
        var store = new DeviceCredentialStore(_dir);
        var credential = new DeviceCredential(Guid.NewGuid(), "pdk_secret-key");

        store.Save(credential);

        Assert.Equal(credential, store.Load());
        var onDisk = File.ReadAllText(Path.Combine(_dir, "credentials.bin"));
        Assert.DoesNotContain("pdk_secret-key", onDisk); // DPAPI, not plain JSON

        store.Forget();
        Assert.Null(store.Load());
    }

    [Fact]
    public void A_corrupt_file_reads_as_not_paired()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(Path.Combine(_dir, "credentials.bin"), "garbage");

        Assert.Null(new DeviceCredentialStore(_dir).Load());
    }

    [Fact]
    public async Task The_device_client_targets_the_server_and_carries_the_key()
    {
        var settings = new DesktopSettingsStore(_dir);
        settings.Current.ServerUrl = "http://homelab:8730/";
        var credentials = new DeviceCredentialStore(_dir);
        credentials.Save(new DeviceCredential(Guid.NewGuid(), "pdk_abc"));

        var capture = new CaptureHandler();
        var services = new ServiceCollection().AddSingleton(settings).AddSingleton(credentials).AddDeviceHttpClient();
        services.AddHttpClient(DesktopHttp.DeviceClient).ConfigurePrimaryHttpMessageHandler(() => capture);
        using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient(DesktopHttp.DeviceClient);

        await client.GetAsync(DeviceCredentialCheck.MeUri);

        Assert.Equal("http://homelab:8730/api/v1/identity/devices/me", capture.Request!.RequestUri!.ToString());
        Assert.Equal("pdk_abc", capture.Request.Headers.GetValues("X-Api-Key").Single());
    }

    private sealed class CaptureHandler : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }
}
