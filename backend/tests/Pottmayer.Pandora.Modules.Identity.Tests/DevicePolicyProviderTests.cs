using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Pottmayer.Pandora.Modules.Identity.Abstractions;
using Pottmayer.Pandora.Modules.Identity.Domain.Entities;
using Pottmayer.Pandora.Modules.Identity.Infrastructure.Security;
using Xunit;

namespace Pottmayer.Pandora.Modules.Identity.Tests;

/// <summary>
/// The device policies a module names on its endpoints: "device" needs a paired device, and
/// "device-scope:x" also needs scope x. No real endpoint asks for a scope yet (Files will), so the
/// rule is pinned here.
/// </summary>
public sealed class DevicePolicyProviderTests
{
    private static IAuthorizationService NewAuthorizationService()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorization();
        services.AddSingleton<IAuthorizationPolicyProvider, DevicePolicyProvider>();
        return services.BuildServiceProvider().GetRequiredService<IAuthorizationService>();
    }

    private static ClaimsPrincipal DeviceWith(params string[] scopes) => new(new ClaimsIdentity(
        [
            new Claim(DeviceAuthorization.DeviceIdClaim, Guid.NewGuid().ToString()),
            .. scopes.Select(s => new Claim(DeviceAuthorization.ScopeClaim, s)),
        ],
        DeviceAuthorization.Scheme));

    private static ClaimsPrincipal SessionUser() =>
        new(new ClaimsIdentity([new Claim("Id", Guid.NewGuid().ToString())], "Bearer"));

    [Fact]
    public async Task Scope_policy_admits_only_a_device_holding_that_scope()
    {
        var auth = NewAuthorizationService();
        const string policy = DeviceAuthorization.ScopePolicyPrefix + "files.agent";

        Assert.True((await auth.AuthorizeAsync(DeviceWith("files.agent"), policy)).Succeeded);
        Assert.False((await auth.AuthorizeAsync(DeviceWith("agenda.notify"), policy)).Succeeded);
        Assert.False((await auth.AuthorizeAsync(DeviceWith(), policy)).Succeeded);
        Assert.False((await auth.AuthorizeAsync(SessionUser(), policy)).Succeeded);
    }

    [Fact]
    public async Task Device_policy_admits_any_device_but_not_a_session()
    {
        var auth = NewAuthorizationService();

        Assert.True((await auth.AuthorizeAsync(DeviceWith(), DeviceAuthorization.Policy)).Succeeded);
        Assert.False((await auth.AuthorizeAsync(SessionUser(), DeviceAuthorization.Policy)).Succeeded);
    }

    [Theory]
    [InlineData("files.agent", true)]
    [InlineData("files", true)]
    [InlineData("agenda.notify-native", true)]
    [InlineData("Files.Agent", false)]
    [InlineData("files..agent", false)]
    [InlineData("files.agent.", false)]
    [InlineData("files agent", false)]
    [InlineData("", false)]
    public void Scope_names_are_lower_case_dot_separated(string scope, bool valid) =>
        Assert.Equal(valid, Device.IsValidScope(scope));
}
