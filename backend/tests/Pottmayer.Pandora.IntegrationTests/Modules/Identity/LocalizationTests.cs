using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Pottmayer.Pandora.IntegrationTests.Support;
using Xunit;

namespace Pottmayer.Pandora.IntegrationTests.Modules.Identity;

/// <summary>
/// Verifies that error messages are localized from the request's Accept-Language header (en default,
/// pt-BR when requested), resolved by error code through the message resources.
/// </summary>
[Collection("Integration")]
public sealed class LocalizationTests : IAsyncLifetime
{
    private const string SignInUrl = "/api/v1/identity/auth/signin";

    private readonly PandoraWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public LocalizationTests(PandoraWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public Task InitializeAsync() => _factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Error_message_is_in_portuguese_when_accept_language_is_pt_br()
    {
        await IdentityHelper.RegisterActiveUserAsync(_client, _factory.ConnectionString, "alice@example.com", "alice");

        var request = new HttpRequestMessage(HttpMethod.Post, SignInUrl)
        {
            Content = JsonContent.Create(new { emailOrUsername = "alice", password = "wrong" }),
        };
        request.Headers.AcceptLanguage.Add(new StringWithQualityHeaderValue("pt-BR"));

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorEnvelope>();
        Assert.Equal("As credenciais informadas são inválidas.", error!.ErrorMessage);
    }

    [Fact]
    public async Task Error_message_defaults_to_english()
    {
        await IdentityHelper.RegisterActiveUserAsync(_client, _factory.ConnectionString, "bob@example.com", "bob");

        var response = await _client.PostAsJsonAsync(SignInUrl, new { emailOrUsername = "bob", password = "wrong" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorEnvelope>();
        Assert.Equal("The provided credentials are invalid.", error!.ErrorMessage);
    }

    private sealed record ErrorEnvelope(string ErrorCode, string ErrorMessage);
}
