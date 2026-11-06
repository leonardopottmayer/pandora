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

/// <summary>The panel's endpoints: the MFA gate, flags, roles, grants, the last-admin guard and the audit.</summary>
[Collection("Integration")]
public sealed class AdminPanelTests : IAsyncLifetime
{
    private const string Admin = "/api/v1/admin";
    private const string AdminRoleId = "019a5a00-0000-7000-8000-000000000001";

    private readonly PandoraWebApplicationFactory _factory;
    private readonly HttpClient _boss;
    private readonly HttpClient _member;

    public AdminPanelTests(PandoraWebApplicationFactory factory)
    {
        _factory = factory;
        _boss = factory.CreateClient();
        _member = factory.CreateClient();
    }

    public Task InitializeAsync() => _factory.ResetDatabaseAsync();

    // Respawn keeps adm002/adm003 for the seeded roles, so the roles a test creates are removed here;
    // flag overrides are cached for the whole host.
    public async Task DisposeAsync()
    {
        await ExecuteAsync("DELETE FROM admin.adm002_role WHERE NOT is_system AND NOT is_default");
        _factory.Services.GetRequiredService<IFeatureFlags>().Invalidate();
        _factory.Services.GetRequiredService<IPermissionChecker>().InvalidateAll();
    }

    [Fact]
    public async Task The_panel_needs_two_step_verification_and_an_admin_permission()
    {
        await IdentityHelper.AuthenticateAsync(_boss, _factory.ConnectionString, "boss@pandora.test", "boss");
        await BootstrapAsync();

        var withoutMfa = await _boss.GetAsync($"{Admin}/users");
        Assert.Equal(HttpStatusCode.Forbidden, withoutMfa.StatusCode);
        Assert.Contains("Admin.MfaRequired", await withoutMfa.Content.ReadAsStringAsync());

        await TurnOnMfaAsync("boss@pandora.test");
        Assert.Equal(HttpStatusCode.OK, (await _boss.GetAsync($"{Admin}/users")).StatusCode);

        await IdentityHelper.AuthenticateAsync(_member, _factory.ConnectionString, "member@pandora.test", "member");
        await TurnOnMfaAsync("member@pandora.test");
        var member = await _member.GetAsync($"{Admin}/users");
        Assert.Equal(HttpStatusCode.Forbidden, member.StatusCode);
        Assert.Contains(AuthorizationErrors.Denied, await member.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Switching_a_feature_off_hides_it_until_it_is_back_on()
    {
        await AdminAsync();
        await IdentityHelper.AuthenticateAsync(_member, _factory.ConnectionString, "member@pandora.test", "member");

        var off = await _boss.PutAsJsonAsync($"{Admin}/features/notes", new { enabled = false });
        Assert.Equal(HttpStatusCode.OK, off.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _member.GetAsync("/api/v1/notes/spaces")).StatusCode);
        Assert.Equal(1L, await ScalarAsync("SELECT count(*) FROM admin.adm001_feature_flag WHERE key = 'notes'"));

        await _boss.PutAsJsonAsync($"{Admin}/features/notes", new { enabled = true });
        Assert.Equal(HttpStatusCode.OK, (await _member.GetAsync("/api/v1/notes/spaces")).StatusCode);
        // Back at its default, the override is gone; both changes are audited.
        Assert.Equal(0L, await ScalarAsync("SELECT count(*) FROM admin.adm001_feature_flag"));
        Assert.Equal(2L, await ScalarAsync("SELECT count(*) FROM admin.adm006_audit_entry WHERE action = 'feature.changed'"));
    }

    [Fact]
    public async Task A_new_role_reaches_its_holders_at_once()
    {
        await AdminAsync();
        var memberId = await AuthenticateMemberWithMfaAsync();

        var invalid = await _boss.PostAsJsonAsync($"{Admin}/roles", new { name = "Auditor", patterns = new[] { "admin.nothing.*" } });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, invalid.StatusCode);

        var created = await _boss.PostAsJsonAsync($"{Admin}/roles", new { name = "Auditor", patterns = new[] { "admin.audit.read" } });
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        var roleId = (await created.Content.ReadFromJsonAsync<Envelope<Guid>>())!.Data;

        Assert.Equal(HttpStatusCode.Forbidden, (await _member.GetAsync($"{Admin}/audit")).StatusCode);
        var assigned = await _boss.PostAsJsonAsync($"{Admin}/users/roles", new { userIds = new[] { memberId }, roleId, assign = true });
        Assert.Equal(1, (await assigned.Content.ReadFromJsonAsync<Envelope<int>>())!.Data);
        Assert.Equal(HttpStatusCode.OK, (await _member.GetAsync($"{Admin}/audit")).StatusCode);

        await _boss.DeleteAsync($"{Admin}/roles/{roleId}");
        Assert.Equal(HttpStatusCode.Forbidden, (await _member.GetAsync($"{Admin}/audit")).StatusCode);
    }

    [Fact]
    public async Task A_deny_exception_shows_in_the_detail_and_blocks_the_request()
    {
        await AdminAsync();
        var memberId = await AuthenticateMemberWithMfaAsync();

        var set = await _boss.PutAsJsonAsync($"{Admin}/users/{memberId}/exceptions",
            new { exceptions = new[] { new { pattern = "finances.accounts.*", effect = "deny" } } });
        Assert.Equal(HttpStatusCode.OK, set.StatusCode);

        Assert.Equal(HttpStatusCode.Forbidden, (await _member.GetAsync("/api/v1/finances/accounts")).StatusCode);
        var detail = (await _boss.GetFromJsonAsync<Envelope<UserDetail>>($"{Admin}/users/{memberId}"))!.Data;
        var read = Assert.Single(detail.Permissions, p => p.Key == "finances.accounts.read");
        Assert.False(read.Granted);
        Assert.True(read.Denied);
        Assert.Equal(["member"], read.Roles);
    }

    [Fact]
    public async Task The_last_admin_keeps_the_role_and_cannot_be_disabled()
    {
        var bossId = await AdminAsync();

        var remove = await _boss.PostAsJsonAsync($"{Admin}/users/roles",
            new { userIds = new[] { bossId }, roleId = Guid.Parse(AdminRoleId), assign = false });
        Assert.Equal(HttpStatusCode.Conflict, remove.StatusCode);
        Assert.Contains("Admin.LastAdmin", await remove.Content.ReadAsStringAsync());

        var disable = await _boss.PutAsJsonAsync($"{Admin}/users/{bossId}/disabled", new { disabled = true });
        Assert.Equal(HttpStatusCode.Conflict, disable.StatusCode);
    }

    [Fact]
    public async Task A_disabled_account_cannot_sign_in_until_enabled()
    {
        await AdminAsync();
        var memberId = await AuthenticateMemberWithMfaAsync();

        Assert.Equal(HttpStatusCode.OK, (await _boss.PutAsJsonAsync($"{Admin}/users/{memberId}/disabled", new { disabled = true })).StatusCode);
        Assert.False((await IdentityHelper.PostSignInAsync(_member, "member", IdentityHelper.DefaultPassword)).IsSuccessStatusCode);

        await _boss.PutAsJsonAsync($"{Admin}/users/{memberId}/disabled", new { disabled = false });
        Assert.True((await IdentityHelper.PostSignInAsync(_member, "member", IdentityHelper.DefaultPassword)).IsSuccessStatusCode);

        var audit = (await _boss.GetFromJsonAsync<Envelope<AuditRow[]>>($"{Admin}/audit?targetId={memberId}"))!.Data;
        Assert.Equal(["user.enabled", "user.disabled"], audit.Select(a => a.Action).Take(2));
        Assert.All(audit.Take(2), a => Assert.Equal("Test User", a.TargetName));
    }

    /// <summary>The bootstrap admin, signed in on <see cref="_boss"/> with MFA on.</summary>
    private async Task<Guid> AdminAsync()
    {
        await IdentityHelper.AuthenticateAsync(_boss, _factory.ConnectionString, "boss@pandora.test", "boss");
        await BootstrapAsync();
        await TurnOnMfaAsync("boss@pandora.test");
        return await IdAsync("boss@pandora.test");
    }

    private async Task<Guid> AuthenticateMemberWithMfaAsync()
    {
        await IdentityHelper.AuthenticateAsync(_member, _factory.ConnectionString, "member@pandora.test", "member");
        await TurnOnMfaAsync("member@pandora.test");
        return await IdAsync("member@pandora.test");
    }

    private async Task BootstrapAsync()
    {
        using var scope = _factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AdminBootstrapper>().RunAsync();
    }

    // Turned on in the table: the tokens already issued stay valid, and the panel reads the flag live.
    private Task TurnOnMfaAsync(string email) =>
        ExecuteAsync("UPDATE identity.idt001_user SET mfa_enabled = true WHERE email = $1", email);

    private async Task<Guid> IdAsync(string email)
    {
        await using var conn = new NpgsqlConnection(_factory.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand("SELECT id FROM identity.idt001_user WHERE email = $1", conn);
        cmd.Parameters.AddWithValue(email);
        return (Guid)(await cmd.ExecuteScalarAsync())!;
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
    private sealed record UserDetail(Permission[] Permissions);
    private sealed record Permission(string Key, bool Granted, string[] Roles, bool Allowed, bool Denied);
    private sealed record AuditRow(string Action, string? TargetName);
}
