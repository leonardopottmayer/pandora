using System.Net;
using System.Net.Http.Json;
using Pottmayer.Pandora.IntegrationTests.Support;
using Xunit;

namespace Pottmayer.Pandora.IntegrationTests.Modules.Identity;

/// <summary>
/// Covers the account-activation endpoint using the real token: sign-up enqueues an activation
/// notification whose payload carries the plaintext token, which the user then redeems.
/// </summary>
[Collection("Integration")]
public sealed class AccountActivationTests : IAsyncLifetime
{
    private const string SignUpUrl = "/api/v1/identity/auth/signup";
    private const string SignInUrl = "/api/v1/identity/auth/signin";
    private const string ActivateUrl = "/api/v1/identity/auth/activate";

    private readonly PandoraWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private readonly NotificationsProbe _notifications;

    public AccountActivationTests(PandoraWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _notifications = new NotificationsProbe(factory.ConnectionString);
    }

    public Task InitializeAsync() => _factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Activate_with_the_emailed_token_lets_the_user_sign_in()
    {
        var email = "alice@example.com";
        await SignUpAsync(email, "alice");
        await _notifications.WaitForRecipientAsync(email);
        var token = await _notifications.GetActivationTokenAsync(email);

        // Pending account cannot sign in yet.
        var beforeSignIn = await _client.PostAsJsonAsync(SignInUrl, new { emailOrUsername = "alice", password = IdentityHelper.DefaultPassword });
        Assert.Equal(HttpStatusCode.Unauthorized, beforeSignIn.StatusCode);

        var activate = await _client.PostAsJsonAsync(ActivateUrl, new { token });
        Assert.True(activate.IsSuccessStatusCode, await activate.Content.ReadAsStringAsync());

        // Now the account is active and can authenticate.
        var afterSignIn = await _client.PostAsJsonAsync(SignInUrl, new { emailOrUsername = "alice", password = IdentityHelper.DefaultPassword });
        Assert.Equal(HttpStatusCode.OK, afterSignIn.StatusCode);
    }

    [Fact]
    public async Task Activate_with_an_invalid_token_is_rejected()
    {
        var response = await _client.PostAsJsonAsync(ActivateUrl, new { token = "not-a-real-token" });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task Activate_twice_with_the_same_token_is_rejected_the_second_time()
    {
        var email = "bob@example.com";
        await SignUpAsync(email, "bob");
        await _notifications.WaitForRecipientAsync(email);
        var token = await _notifications.GetActivationTokenAsync(email);

        var first = await _client.PostAsJsonAsync(ActivateUrl, new { token });
        Assert.True(first.IsSuccessStatusCode);

        var second = await _client.PostAsJsonAsync(ActivateUrl, new { token });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, second.StatusCode);
    }

    private Task<HttpResponseMessage> SignUpAsync(string email, string username)
        => _client.PostAsJsonAsync(SignUpUrl, new
        {
            name = "Test User",
            username,
            email,
            password = IdentityHelper.DefaultPassword
        });
}
