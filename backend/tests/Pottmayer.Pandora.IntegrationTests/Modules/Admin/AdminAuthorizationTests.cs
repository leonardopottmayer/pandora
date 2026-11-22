using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Pottmayer.Pandora.IntegrationTests.Support;
using Pottmayer.Pandora.Modules.Admin.Application.Bootstrap;
using Pottmayer.Tars.FeatureFlags.Abstractions;
using Pottmayer.Tars.Security.Authorization.Abstractions;
using Xunit;

namespace Pottmayer.Pandora.IntegrationTests.Modules.Admin;

/// <summary>
/// The checkpoint end to end: the feature flag first (404), then the permission (403), with grants read
/// from the admin tables; what <c>/me</c> reports; and the master admin bootstrap.
/// </summary>
[Collection("Integration")]
public sealed class AdminAuthorizationTests : IAsyncLifetime
{
    private const string Accounts = "/api/v1/finances/accounts";

    private readonly PandoraWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public AdminAuthorizationTests(PandoraWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public Task InitializeAsync() => _factory.ResetDatabaseAsync();

    // Flag overrides are cached for the whole host; drop them so no other test sees a disabled feature.
    public Task DisposeAsync()
    {
        _factory.Services.GetRequiredService<IFeatureFlags>().Invalidate();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task A_member_uses_every_module_and_me_reports_it()
    {
        await IdentityHelper.AuthenticateAsync(_client, _factory.ConnectionString, "member@pandora.test", "member");

        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync(Accounts)).StatusCode);

        var me = await MeAsync();
        Assert.Contains("finances", me.Features);
        Assert.Contains("finances.cards.delete", me.Permissions);
        Assert.Contains("notes.pages.create", me.Permissions);
        Assert.DoesNotContain(me.Permissions, p => p.StartsWith("admin.", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_user_without_a_role_is_refused_with_403()
    {
        var userId = await AuthenticateAsync("norole@pandora.test", "norole");
        await ExecuteAsync("DELETE FROM admin.adm004_user_role WHERE user_id = $1", userId);
        Invalidate(userId);

        var response = await _client.GetAsync(Accounts);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Contains(AuthorizationErrors.Denied, await response.Content.ReadAsStringAsync());
        Assert.Empty((await MeAsync()).Permissions);
    }

    [Fact]
    public async Task A_deny_exception_beats_the_role()
    {
        var userId = await AuthenticateAsync("denied@pandora.test", "denied");
        await ExecuteAsync(
            "INSERT INTO admin.adm005_user_permission (user_id, pattern, effect) VALUES ($1, 'finances.accounts.*', 'deny')", userId);
        Invalidate(userId);

        Assert.Equal(HttpStatusCode.Forbidden, (await _client.GetAsync(Accounts)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync("/api/v1/finances/cards")).StatusCode);
    }

    [Fact]
    public async Task A_disabled_feature_is_404_even_for_the_admin()
    {
        await AuthenticateAsync("boss@pandora.test", "boss");
        await BootstrapAsync();
        await ExecuteAsync("INSERT INTO admin.adm001_feature_flag (key, enabled) VALUES ('finances', false)");
        _factory.Services.GetRequiredService<IFeatureFlags>().Invalidate();

        var response = await _client.GetAsync(Accounts);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains(FeatureFlagErrors.Disabled, await response.Content.ReadAsStringAsync());
        var me = await MeAsync();
        Assert.DoesNotContain("finances", me.Features);
        Assert.DoesNotContain("finances.import", me.Features);
        Assert.Contains("admin.users.manage", me.Permissions);
    }

    [Fact]
    public async Task The_bootstrap_promotes_only_the_configured_email_once_and_audits_it()
    {
        await IdentityHelper.RegisterActiveUserAsync(_client, _factory.ConnectionString, "other@pandora.test", "other");
        await AuthenticateAsync("boss@pandora.test", "boss");

        await BootstrapAsync();
        await BootstrapAsync();

        Assert.Contains("admin.roles.manage", (await MeAsync()).Permissions);
        Assert.Equal(1L, await ScalarAsync(
            "SELECT count(*) FROM admin.adm004_user_role WHERE role_id = '019a5a00-0000-7000-8000-000000000001'"));
        Assert.Equal(1L, await ScalarAsync(
            "SELECT count(*) FROM admin.adm006_audit_entry WHERE action = 'role.assigned' AND actor_id IS NULL"));
    }

    private async Task<Guid> AuthenticateAsync(string email, string username)
    {
        await IdentityHelper.AuthenticateAsync(_client, _factory.ConnectionString, email, username);
        return Guid.Parse((await MeAsync()).Id);
    }

    private async Task BootstrapAsync()
    {
        using var scope = _factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AdminBootstrapper>().RunAsync();
    }

    private void Invalidate(Guid userId) =>
        _factory.Services.GetRequiredService<IPermissionChecker>().Invalidate(userId.ToString());

    private async Task<MeDto> MeAsync()
    {
        var envelope = await _client.GetFromJsonAsync<Envelope<MeDto>>("/api/v1/identity/me");
        return envelope!.Data;
    }

    private async Task ExecuteAsync(string sql, params object[] args)
    {
        await using var conn = new NpgsqlConnection(_factory.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(sql, conn);
        foreach (var arg in args)
            cmd.Parameters.AddWithValue(arg);
        await cmd.ExecuteNonQueryAsync();
    }

    private async Task<long> ScalarAsync(string sql)
    {
        await using var conn = new NpgsqlConnection(_factory.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(sql, conn);
        return (long)(await cmd.ExecuteScalarAsync())!;
    }

    private sealed record Envelope<T>(T Data);
    private sealed record MeDto(string Id, string[] Features, string[] Permissions);
}
