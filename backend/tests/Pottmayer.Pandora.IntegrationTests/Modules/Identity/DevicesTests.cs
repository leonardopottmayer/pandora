using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Pottmayer.Pandora.IntegrationTests.Support;
using Xunit;

namespace Pottmayer.Pandora.IntegrationTests.Modules.Identity;

/// <summary>
/// Device keys (D2): pairing with the session returns a key once; the key authenticates only on the
/// endpoints that opt in to the device scheme, never on session endpoints, and dies with revocation.
/// </summary>
[Collection("Integration")]
public sealed class DevicesTests : IAsyncLifetime
{
    private const string DevicesUrl = "/api/v1/identity/devices";
    private const string DeviceMeUrl = "/api/v1/identity/devices/me";
    private const string UserMeUrl = "/api/v1/identity/me";

    private readonly PandoraWebApplicationFactory _factory;
    private readonly HttpClient _user;   // signed-in session
    private readonly HttpClient _device; // device key only

    public DevicesTests(PandoraWebApplicationFactory factory)
    {
        _factory = factory;
        _user = factory.CreateClient();
        _device = factory.CreateClient();
    }

    public Task InitializeAsync() => _factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task A_paired_key_reaches_device_endpoints_only_until_revoked()
    {
        await IdentityHelper.AuthenticateAsync(_user, _factory.ConnectionString, "alice@example.com", "alice");
        var paired = await PairAsync(scopes: ["files.agent"]);
        _device.DefaultRequestHeaders.Add("X-Api-Key", paired.Key);

        // The device sees itself.
        var me = await _device.GetAsync(DeviceMeUrl);
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        var device = (await me.Content.ReadFromJsonAsync<Envelope<DeviceData>>())!.Data;
        Assert.Equal(paired.Device.Id, device.Id);
        Assert.Equal(["files.agent"], device.Scopes);
        Assert.NotNull(device.LastSeenAt);

        // A device key is not a session: session endpoints reject it.
        Assert.Equal(HttpStatusCode.Unauthorized, (await _device.GetAsync(UserMeUrl)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _device.GetAsync(DevicesUrl)).StatusCode);

        // And a session is not a device: the device endpoint rejects the user's token.
        Assert.Equal(HttpStatusCode.Unauthorized, (await _user.GetAsync(DeviceMeUrl)).StatusCode);

        // Revoked: the key stops working and the device leaves the list.
        var revoke = await _user.DeleteAsync($"{DevicesUrl}/{paired.Device.Id}");
        Assert.Equal(HttpStatusCode.OK, revoke.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _device.GetAsync(DeviceMeUrl)).StatusCode);
        var list = (await (await _user.GetAsync(DevicesUrl)).Content.ReadFromJsonAsync<Envelope<List<DeviceData>>>())!.Data;
        Assert.Empty(list);
    }

    [Fact]
    public async Task Unknown_key_is_rejected()
    {
        _device.DefaultRequestHeaders.Add("X-Api-Key", "pdk_not-a-real-key");

        Assert.Equal(HttpStatusCode.Unauthorized, (await _device.GetAsync(DeviceMeUrl)).StatusCode);
    }

    [Fact]
    public async Task Pairing_validates_platform_and_scopes()
    {
        await IdentityHelper.AuthenticateAsync(_user, _factory.ConnectionString, "alice@example.com", "alice");

        var badPlatform = await _user.PostAsJsonAsync(DevicesUrl, new { name = "PC", platform = "amiga", form = "desktop", scopes = Array.Empty<string>() });
        var badScope = await _user.PostAsJsonAsync(DevicesUrl, new { name = "PC", platform = "windows", form = "desktop", scopes = new[] { "Files Agent" } });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, badPlatform.StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, badScope.StatusCode);
    }

    [Fact]
    public async Task Another_users_device_cannot_be_revoked()
    {
        await IdentityHelper.AuthenticateAsync(_user, _factory.ConnectionString, "alice@example.com", "alice");
        var alices = await PairAsync(scopes: []);

        var bob = _factory.CreateClient();
        await IdentityHelper.AuthenticateAsync(bob, _factory.ConnectionString, "bob@example.com", "bob");

        Assert.Equal(HttpStatusCode.NotFound, (await bob.DeleteAsync($"{DevicesUrl}/{alices.Device.Id}")).StatusCode);
    }

    private async Task<Registration> PairAsync(string[] scopes)
    {
        var response = await _user.PostAsJsonAsync(DevicesUrl, new { name = "HOMELAB", platform = "windows", form = "desktop", scopes });
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        var registration = (await response.Content.ReadFromJsonAsync<Envelope<Registration>>())!.Data;
        Assert.StartsWith("pdk_", registration.Key);
        return registration;
    }

    private sealed record Envelope<T>(T Data);
    private sealed record Registration(DeviceData Device, string Key);
    private sealed record DeviceData(Guid Id, string Name, string Platform, string Form, string[] Scopes, DateTimeOffset? LastSeenAt);
}
