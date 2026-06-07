using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using OtpNet;
using Pottmayer.Pandora.IntegrationTests.Support;
using Xunit;

namespace Pottmayer.Pandora.IntegrationTests.Modules.Identity;

/// <summary>
/// Covers the MFA (TOTP) lifecycle end-to-end against the real Host and database: setup → enable,
/// the sign-in challenge, recovery-code fallback (single use), and disable.
/// </summary>
[Collection("Integration")]
public sealed class MfaTests : IAsyncLifetime
{
    private const string SetupUrl = "/api/v1/identity/mfa/setup";
    private const string EnableUrl = "/api/v1/identity/mfa/enable";
    private const string DisableUrl = "/api/v1/identity/mfa/disable";
    private const string ChallengeUrl = "/api/v1/identity/mfa/challenge";

    private readonly PandoraWebApplicationFactory _factory;
    private readonly NotificationsProbe _notifications;

    public MfaTests(PandoraWebApplicationFactory factory)
    {
        _factory = factory;
        _notifications = new NotificationsProbe(factory.ConnectionString);
    }

    public Task InitializeAsync() => _factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Enable_then_signin_requires_and_completes_the_mfa_challenge()
    {
        var authed = _factory.CreateClient();
        await IdentityHelper.AuthenticateAsync(authed, _factory.ConnectionString, "alice@example.com", "alice");

        var secret = await SetupAsync(authed);
        await EnableAsync(authed, secret);

        // A fresh sign-in now stops at the MFA challenge instead of returning tokens.
        var anon = _factory.CreateClient();
        var signIn = await IdentityHelper.PostSignInAsync(anon, "alice", IdentityHelper.DefaultPassword);
        Assert.Equal(HttpStatusCode.OK, signIn.StatusCode);
        var ticket = await IdentityHelper.ReadMfaTicketAsync(signIn);
        Assert.False(string.IsNullOrWhiteSpace(ticket));

        // Completing the challenge with a valid TOTP yields tokens.
        var challenge = await anon.PostAsJsonAsync(ChallengeUrl, new { ticket, code = Code(secret) });
        Assert.Equal(HttpStatusCode.OK, challenge.StatusCode);
        var tokens = await IdentityHelper.ReadTokensAsync(challenge);
        Assert.False(string.IsNullOrWhiteSpace(tokens.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(tokens.RefreshToken));

        await _notifications.WaitForTemplateAsync("alice@example.com", "mfa-enabled");
    }

    [Fact]
    public async Task Recovery_code_completes_the_challenge_and_is_single_use()
    {
        var authed = _factory.CreateClient();
        await IdentityHelper.AuthenticateAsync(authed, _factory.ConnectionString, "bob@example.com", "bob");

        var secret = await SetupAsync(authed);
        var recoveryCodes = await EnableAsync(authed, secret);
        var recoveryCode = recoveryCodes[0];

        // First use: a recovery code redeems the challenge.
        var anon = _factory.CreateClient();
        var firstSignIn = await IdentityHelper.PostSignInAsync(anon, "bob", IdentityHelper.DefaultPassword);
        var firstTicket = await IdentityHelper.ReadMfaTicketAsync(firstSignIn);
        var firstChallenge = await anon.PostAsJsonAsync(ChallengeUrl, new { ticket = firstTicket, code = recoveryCode });
        Assert.Equal(HttpStatusCode.OK, firstChallenge.StatusCode);

        // Second use of the same code is rejected.
        var secondSignIn = await IdentityHelper.PostSignInAsync(anon, "bob", IdentityHelper.DefaultPassword);
        var secondTicket = await IdentityHelper.ReadMfaTicketAsync(secondSignIn);
        var secondChallenge = await anon.PostAsJsonAsync(ChallengeUrl, new { ticket = secondTicket, code = recoveryCode });
        Assert.Equal(HttpStatusCode.Unauthorized, secondChallenge.StatusCode);
    }

    [Fact]
    public async Task Disable_requires_password_and_code_then_signin_skips_mfa()
    {
        var authed = _factory.CreateClient();
        await IdentityHelper.AuthenticateAsync(authed, _factory.ConnectionString, "carol@example.com", "carol");

        var secret = await SetupAsync(authed);
        await EnableAsync(authed, secret);

        // Wrong code is rejected.
        var badDisable = await authed.PostAsJsonAsync(DisableUrl,
            new { password = IdentityHelper.DefaultPassword, code = "000000" });
        Assert.Equal(HttpStatusCode.Unauthorized, badDisable.StatusCode);

        // Correct password + TOTP disables MFA.
        var disable = await authed.PostAsJsonAsync(DisableUrl,
            new { password = IdentityHelper.DefaultPassword, code = Code(secret) });
        Assert.Equal(HttpStatusCode.OK, disable.StatusCode);

        // Sign-in now returns tokens directly, no challenge.
        var anon = _factory.CreateClient();
        var signIn = await IdentityHelper.PostSignInAsync(anon, "carol", IdentityHelper.DefaultPassword);
        Assert.Equal(HttpStatusCode.OK, signIn.StatusCode);
        Assert.Null(await IdentityHelper.ReadMfaTicketAsync(signIn));

        await _notifications.WaitForTemplateAsync("carol@example.com", "mfa-disabled");
    }

    [Fact]
    public async Task Enable_with_a_wrong_code_is_rejected()
    {
        var authed = _factory.CreateClient();
        await IdentityHelper.AuthenticateAsync(authed, _factory.ConnectionString, "dave@example.com", "dave");

        await SetupAsync(authed);
        var enable = await authed.PostAsJsonAsync(EnableUrl, new { code = "000000" });

        Assert.Equal(HttpStatusCode.Unauthorized, enable.StatusCode);
    }

    // -------------------------------------------------------------------- helpers

    private static async Task<string> SetupAsync(HttpClient client)
    {
        var response = await client.PostAsync(SetupUrl, content: null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var envelope = await response.Content.ReadFromJsonAsync<Env<SetupData>>();
        return envelope!.Data.Secret;
    }

    private static async Task<string[]> EnableAsync(HttpClient client, string secret)
    {
        var response = await client.PostAsJsonAsync(EnableUrl, new { code = Code(secret) });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var envelope = await response.Content.ReadFromJsonAsync<Env<RecoveryData>>();
        return envelope!.Data.RecoveryCodes;
    }

    private static string Code(string secret) =>
        new Totp(Base32Encoding.ToBytes(secret)).ComputeTotp();

    private sealed record Env<T>(T Data);
    private sealed record SetupData(string Secret, string OtpauthUri);
    private sealed record RecoveryData(string[] RecoveryCodes);
}
