using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Pottmayer.Pandora.IntegrationTests.Support;
using Xunit;

namespace Pottmayer.Pandora.IntegrationTests.Modules.Identity;

/// <summary>
/// Exercises the authentication endpoints against the real Host and database: sign-in issues
/// tokens, refresh rotates them, and sign-out revokes the refresh token.
/// </summary>
[Collection("Integration")]
public sealed class IdentityAuthTests : IAsyncLifetime
{
    private const string SignInUrl = "/api/v1/identity/auth/signin";
    private const string RefreshUrl = "/api/v1/identity/auth/refresh";
    private const string SignOutUrl = "/api/v1/identity/auth/signout";

    private readonly PandoraWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public IdentityAuthTests(PandoraWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public Task InitializeAsync() => _factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    // ---------------------------------------------------------------- SIGN-IN

    [Fact]
    public async Task SignIn_returns_tokens_for_valid_credentials()
    {
        await IdentityHelper.RegisterActiveUserAsync(_client, _factory.ConnectionString, "alice@example.com", "alice");

        var tokens = await IdentityHelper.SignInAsync(_client, "alice");

        Assert.False(string.IsNullOrWhiteSpace(tokens.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(tokens.RefreshToken));
    }

    [Fact]
    public async Task SignIn_returns_unauthorized_for_wrong_password()
    {
        await IdentityHelper.RegisterActiveUserAsync(_client, _factory.ConnectionString, "bob@example.com", "bob");

        var response = await _client.PostAsJsonAsync(SignInUrl, new { emailOrUsername = "bob", password = "wrong" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task SignIn_returns_unauthorized_for_unknown_user()
    {
        var response = await _client.PostAsJsonAsync(SignInUrl, new { emailOrUsername = "ghost", password = "whatever" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task SignIn_returns_unauthorized_for_a_pending_account()
    {
        // Sign up without activating: the account cannot authenticate yet.
        await _client.PostAsJsonAsync("/api/v1/identity/auth/signup", new
        {
            name = "Pending User",
            username = "pending",
            email = "pending@example.com",
            password = IdentityHelper.DefaultPassword
        });

        var response = await _client.PostAsJsonAsync(SignInUrl, new { emailOrUsername = "pending", password = IdentityHelper.DefaultPassword });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ----------------------------------------------------------------- REFRESH

    [Fact]
    public async Task Refresh_returns_new_tokens_for_a_valid_refresh_token()
    {
        await IdentityHelper.RegisterActiveUserAsync(_client, _factory.ConnectionString, "carol@example.com", "carol");
        var tokens = await IdentityHelper.SignInAsync(_client, "carol");

        var response = await _client.PostAsJsonAsync(RefreshUrl, new { refreshToken = tokens.RefreshToken });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var refreshed = await IdentityHelper.ReadTokensAsync(response);
        Assert.False(string.IsNullOrWhiteSpace(refreshed.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(refreshed.RefreshToken));
    }

    [Fact]
    public async Task Refresh_returns_unauthorized_for_an_invalid_token()
    {
        var response = await _client.PostAsJsonAsync(RefreshUrl, new { refreshToken = "not-a-real-token" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ----------------------------------------------------------------- SIGN-OUT

    [Fact]
    public async Task SignOut_returns_unauthorized_without_an_access_token()
    {
        var response = await _client.PostAsJsonAsync(SignOutUrl, new { refreshToken = "anything" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task SignOut_revokes_the_refresh_token()
    {
        await IdentityHelper.RegisterActiveUserAsync(_client, _factory.ConnectionString, "dave@example.com", "dave");
        var tokens = await IdentityHelper.SignInAsync(_client, "dave");

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        var signOut = await _client.PostAsJsonAsync(SignOutUrl, new { refreshToken = tokens.RefreshToken });
        Assert.Equal(HttpStatusCode.OK, signOut.StatusCode);

        // The revoked refresh token must no longer be accepted.
        _client.DefaultRequestHeaders.Authorization = null;
        var refresh = await _client.PostAsJsonAsync(RefreshUrl, new { refreshToken = tokens.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
    }
}
