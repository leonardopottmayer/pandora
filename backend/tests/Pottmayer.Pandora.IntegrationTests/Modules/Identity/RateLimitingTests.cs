using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Pottmayer.Pandora.IntegrationTests.Support;
using Xunit;

namespace Pottmayer.Pandora.IntegrationTests.Modules.Identity;

/// <summary>The rate limiter, switched on with small limits on a host of its own.</summary>
[Collection("Integration")]
public sealed class RateLimitingTests(PandoraWebApplicationFactory factory) : IAsyncLifetime
{
    public Task InitializeAsync() => factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private WebApplicationFactory<Program> Limited(int authLimit, int apiLimit) =>
        factory.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Pandora:RateLimiting:Enabled"] = "true",
                ["Pandora:RateLimiting:Auth:PermitLimit"] = authLimit.ToString(),
                ["Pandora:RateLimiting:Api:PermitLimit"] = apiLimit.ToString(),
            })));

    [Fact]
    public async Task Sign_in_attempts_past_the_limit_get_429_with_retry_after()
    {
        using var host = Limited(authLimit: 2, apiLimit: 100);
        var client = host.CreateClient();

        for (var i = 0; i < 2; i++)
            Assert.NotEqual(HttpStatusCode.TooManyRequests, (await IdentityHelper.PostSignInAsync(client, "nobody", "wrong")).StatusCode);

        var refused = await IdentityHelper.PostSignInAsync(client, "nobody", "wrong");

        Assert.Equal(HttpStatusCode.TooManyRequests, refused.StatusCode);
        Assert.True(refused.Headers.Contains("Retry-After"));
        Assert.Contains("RateLimit.Exceeded", await refused.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Every_api_request_counts_against_the_global_limit()
    {
        using var host = Limited(authLimit: 100, apiLimit: 3);
        var client = host.CreateClient();

        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 4; i++)
            statuses.Add((await client.GetAsync("/api/v1/identity/me")).StatusCode);

        Assert.Equal([HttpStatusCode.Unauthorized, HttpStatusCode.Unauthorized, HttpStatusCode.Unauthorized, HttpStatusCode.TooManyRequests], statuses);
    }
}
