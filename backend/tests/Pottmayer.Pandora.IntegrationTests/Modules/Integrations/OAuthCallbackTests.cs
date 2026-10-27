using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Pottmayer.Pandora.IntegrationTests.Support;
using Pottmayer.Pandora.Modules.Integrations.Domain.Ports;
using Xunit;

namespace Pottmayer.Pandora.IntegrationTests.Modules.Integrations;

/// <summary>
/// The OAuth connect → callback round trip with a fake Google, for several accounts per provider:
/// adding a second account keeps the first, reconnecting the same identity updates it in place, and a
/// reconnect that comes back as a different identity never overwrites the targeted account.
/// </summary>
[Collection("Integration")]
public sealed class OAuthCallbackTests : IAsyncLifetime
{
    private const string ConnectUrl = "/api/v1/integrations/google/connect";
    private const string RedirectAfter = "/integrations/connections";

    private readonly PandoraWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public OAuthCallbackTests(PandoraWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory
            .WithWebHostBuilder(b => b.ConfigureTestServices(s => s.AddSingleton<IOAuthProvider, FakeGoogle>()))
            .CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
    }

    public Task InitializeAsync() => _factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Connecting_a_second_google_account_keeps_the_first()
    {
        await IdentityHelper.AuthenticateAsync(_client, _factory.ConnectionString, "alice@example.com", "alice");

        var first = await ConnectAsync(FakeGoogle.Code("sub-personal", "me@gmail.com"));
        var second = await ConnectAsync(FakeGoogle.Code("sub-work", "me@work.com"));

        Assert.Equal(RedirectAfter, first);
        Assert.Equal(RedirectAfter, second);
        var accounts = await AccountsApi.AccountsAsync(_client);
        Assert.Equal(["me@gmail.com", "me@work.com"], accounts.Select(a => a.Label).Order());
        Assert.All(accounts, a => Assert.Equal("oauth", a.AuthKind));
    }

    [Fact]
    public async Task Reconnecting_the_same_identity_updates_the_account_and_keeps_its_name()
    {
        await IdentityHelper.AuthenticateAsync(_client, _factory.ConnectionString, "bob@example.com", "bob");
        await ConnectAsync(FakeGoogle.Code("sub-1", "me@gmail.com"));
        var id = Assert.Single(await AccountsApi.AccountsAsync(_client)).Id;
        await _client.PatchAsJsonAsync($"{AccountsApi.AccountsUrl}/{id}", new { label = "Personal" });

        var landing = await ConnectAsync(FakeGoogle.Code("sub-1", "me@gmail.com"), accountId: id);

        Assert.Equal(RedirectAfter, landing);
        var account = Assert.Single(await AccountsApi.AccountsAsync(_client));
        Assert.Equal(id, account.Id);
        Assert.Equal("Personal", account.Label);
    }

    [Fact]
    public async Task A_reconnect_that_comes_back_as_another_identity_leaves_the_target_untouched()
    {
        await IdentityHelper.AuthenticateAsync(_client, _factory.ConnectionString, "carol@example.com", "carol");
        await ConnectAsync(FakeGoogle.Code("sub-target", "target@gmail.com"));
        var target = Assert.Single(await AccountsApi.AccountsAsync(_client));

        var landing = await ConnectAsync(FakeGoogle.Code("sub-other", "other@gmail.com"), accountId: target.Id);

        Assert.Equal($"{RedirectAfter}?integration=other-account", landing);
        var accounts = await AccountsApi.AccountsAsync(_client);
        Assert.Equal(2, accounts.Count);
        Assert.Equal("target@gmail.com", accounts.Single(a => a.Id == target.Id).DisplayName);
    }

    [Fact]
    public async Task A_reconnect_hints_the_targeted_account_to_the_provider()
    {
        await IdentityHelper.AuthenticateAsync(_client, _factory.ConnectionString, "dave@example.com", "dave");
        await ConnectAsync(FakeGoogle.Code("sub-1", "me@gmail.com"));
        var id = Assert.Single(await AccountsApi.AccountsAsync(_client)).Id;

        var adding = await StartAsync(accountId: null);
        var reconnecting = await StartAsync(accountId: id);

        Assert.DoesNotContain("login_hint", adding);
        Assert.Contains("login_hint=me%40gmail.com", reconnecting);
    }

    [Fact]
    public async Task Connect_refuses_to_target_another_users_account()
    {
        await IdentityHelper.AuthenticateAsync(_client, _factory.ConnectionString, "erin@example.com", "erin");
        await ConnectAsync(FakeGoogle.Code("sub-1", "me@gmail.com"));
        var erins = Assert.Single(await AccountsApi.AccountsAsync(_client)).Id;

        var other = _factory
            .WithWebHostBuilder(b => b.ConfigureTestServices(s => s.AddSingleton<IOAuthProvider, FakeGoogle>()))
            .CreateClient();
        await IdentityHelper.AuthenticateAsync(other, _factory.ConnectionString, "frank@example.com", "frank");

        var response = await other.PostAsJsonAsync(ConnectUrl, new { redirectAfter = RedirectAfter, accountId = erins });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>Starts a connection, completes the callback with <paramref name="code"/>, and returns where the browser lands.</summary>
    private async Task<string> ConnectAsync(string code, Guid? accountId = null)
    {
        var url = await StartAsync(accountId);
        var state = System.Web.HttpUtility.ParseQueryString(new Uri(url).Query)["state"];

        var callback = await _client.GetAsync(
            $"/api/v1/integrations/google/callback?code={Uri.EscapeDataString(code)}&state={state}");

        Assert.Equal(HttpStatusCode.Redirect, callback.StatusCode);
        return callback.Headers.Location!.OriginalString;
    }

    private async Task<string> StartAsync(Guid? accountId) =>
        await AccountsApi.ReadDataAsync<string>(
            await _client.PostAsJsonAsync(ConnectUrl, new { redirectAfter = RedirectAfter, accountId }));

    /// <summary>
    /// A Google stand-in: the authorization code carries the identity the "user signed in with", so a
    /// test decides which account comes back.
    /// </summary>
    private sealed class FakeGoogle : IOAuthProvider
    {
        public static string Code(string sub, string email) => $"{sub}|{email}";

        public string Name => "google";

        public IReadOnlyList<string> DefaultScopes => ["openid", "email"];

        public Uri BuildAuthorizationUrl(OAuthAuthorizationRequest request)
        {
            var url = $"https://accounts.fake.test/auth?state={Uri.EscapeDataString(request.State)}";
            if (request.LoginHint is not null)
                url += $"&login_hint={Uri.EscapeDataString(request.LoginHint)}";
            return new Uri(url);
        }

        public Task<OAuthTokens> ExchangeCodeAsync(string code, string codeVerifier, CancellationToken ct = default)
        {
            var (sub, email) = (code.Split('|')[0], code.Split('|')[1]);
            return Task.FromResult(new OAuthTokens(
                $"access-{sub}", DateTimeOffset.UtcNow.AddHours(1), $"refresh-{sub}", DefaultScopes, sub, email));
        }

        public Task<OAuthTokens> RefreshAsync(string refreshToken, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task RevokeAsync(string token, CancellationToken ct = default) => Task.CompletedTask;
    }
}
