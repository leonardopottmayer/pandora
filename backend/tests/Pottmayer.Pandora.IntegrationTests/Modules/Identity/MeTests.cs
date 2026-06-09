using System.Net;
using System.Net.Http.Json;
using Pottmayer.Pandora.IntegrationTests.Support;
using Xunit;

namespace Pottmayer.Pandora.IntegrationTests.Modules.Identity;

/// <summary>
/// Covers the authenticated current-user endpoint (GET /me): the auth requirement and that it
/// returns the signed-in user's profile.
/// </summary>
[Collection("Integration")]
public sealed class MeTests : IAsyncLifetime
{
    private const string MeUrl = "/api/v1/identity/me";

    private readonly PandoraWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public MeTests(PandoraWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public Task InitializeAsync() => _factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Get_returns_unauthorized_without_a_token()
    {
        var response = await _client.GetAsync(MeUrl);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Get_returns_the_authenticated_user()
    {
        await IdentityHelper.AuthenticateAsync(_client, _factory.ConnectionString, "alice@example.com", "alice");

        var response = await _client.GetAsync(MeUrl);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var envelope = await response.Content.ReadFromJsonAsync<Envelope>();
        Assert.NotNull(envelope);
        var me = envelope!.Data;
        Assert.False(string.IsNullOrWhiteSpace(me.Id));
        Assert.Equal("Test User", me.Name);
        Assert.Equal("alice@example.com", me.Email);
        Assert.Equal("alice", me.Username);
    }

    private sealed record Envelope(UserData Data);
    private sealed record UserData(string Id, string Name, string Email, string Username);
}
