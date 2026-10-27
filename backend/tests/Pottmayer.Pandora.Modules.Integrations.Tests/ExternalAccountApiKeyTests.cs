using Pottmayer.Pandora.Modules.Integrations.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Integrations.Domain.ValueObjects;
using Xunit;

namespace Pottmayer.Pandora.Modules.Integrations.Tests;

public sealed class ExternalAccountApiKeyTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 2, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeProvider Clock = new FixedTime(Now);

    [Fact]
    public void ConnectApiKey_creates_an_api_key_account_with_no_expiry_or_refresh()
    {
        var userId = Guid.NewGuid();

        var account = ExternalAccount.ConnectApiKey(userId, "gemini", "ENC(key)", "••••1234", "Google Gemini ••••1234", Clock);

        Assert.Equal(userId, account.UserId);
        Assert.Equal("gemini", account.Provider);
        Assert.Equal(AuthKind.ApiKey, account.AuthKind);
        Assert.Equal(string.Empty, account.Scopes);
        Assert.Equal("ENC(key)", account.AccessTokenEnc);
        Assert.Null(account.AccessTokenExpiresAt);
        Assert.Null(account.RefreshTokenEnc);
        Assert.Equal(AccountStatus.Connected, account.Status);
        Assert.Equal("••••1234", account.DisplayName);
        Assert.Equal("Google Gemini ••••1234", account.Label);
        Assert.Equal(Now, account.ConnectedAt);
        Assert.Equal(Now, account.CreatedAt);
        Assert.NotEqual(Guid.Empty, account.Id);
    }

    [Fact]
    public void Each_api_key_gets_its_own_identity()
    {
        var userId = Guid.NewGuid();

        var first = ExternalAccount.ConnectApiKey(userId, "gemini", "ENC(a)", "••••1111", "A", Clock);
        var second = ExternalAccount.ConnectApiKey(userId, "gemini", "ENC(b)", "••••2222", "B", Clock);

        // A key has no identity at the provider, so the account's own id stands in for it — two keys
        // never collide on (user, provider, provider_account_id).
        Assert.Equal(first.Id.ToString(), first.ProviderAccountId);
        Assert.NotEqual(first.ProviderAccountId, second.ProviderAccountId);
    }

    [Fact]
    public void ReplaceSecret_swaps_the_key_and_clears_a_previous_error()
    {
        var account = ExternalAccount.ConnectApiKey(Guid.NewGuid(), "gemini", "ENC(old)", "••••1111", "Work", Clock);
        account.MarkRevoked("was revoked");

        var later = new FixedTime(Now.AddHours(1));
        account.ReplaceSecret("ENC(new)", "••••2222", later);

        Assert.Equal("ENC(new)", account.AccessTokenEnc);
        Assert.Equal("••••2222", account.DisplayName);
        Assert.Equal("Work", account.Label);
        Assert.Equal(AccountStatus.Connected, account.Status);
        Assert.Null(account.LastError);
        Assert.Equal(Now.AddHours(1), account.LastRefreshedAt);
    }

    [Fact]
    public void ConnectPassword_uses_the_username_as_identity_and_default_label()
    {
        var account = ExternalAccount.ConnectPassword(Guid.NewGuid(), "titan", "me@example.com", "ENC(pw)", null, Clock);

        Assert.Equal(AuthKind.Password, account.AuthKind);
        Assert.Equal("me@example.com", account.ProviderAccountId);
        Assert.Equal("me@example.com", account.DisplayName);
        Assert.Equal("me@example.com", account.Label);
        Assert.Equal("ENC(pw)", account.AccessTokenEnc);
    }

    [Fact]
    public void ReplaceSecret_on_a_password_keeps_the_username_hint()
    {
        var account = ExternalAccount.ConnectPassword(Guid.NewGuid(), "titan", "me@example.com", "ENC(old)", "Exodyas", Clock);

        account.ReplaceSecret("ENC(new)", displayName: null, Clock);

        Assert.Equal("ENC(new)", account.AccessTokenEnc);
        Assert.Equal("me@example.com", account.DisplayName);
        Assert.Equal("Exodyas", account.Label);
    }

    [Fact]
    public void ReplaceSecret_refuses_an_oauth_account()
    {
        var account = ExternalAccount.ConnectOAuth(
            Guid.NewGuid(), "google", "sub-1", "me@gmail.com", "email", "ENC(at)", Now.AddHours(1), "ENC(rt)", Clock);

        Assert.Throws<InvalidOperationException>(() => account.ReplaceSecret("ENC(x)", null, Clock));
    }

    [Fact]
    public void ConnectOAuth_names_the_account_after_its_email()
    {
        var account = ExternalAccount.ConnectOAuth(
            Guid.NewGuid(), "google", "sub-1", "me@gmail.com", "email", "ENC(at)", Now.AddHours(1), "ENC(rt)", Clock);

        Assert.Equal("me@gmail.com", account.Label);
    }

    [Fact]
    public void Rename_trims_and_caps_the_label()
    {
        var account = ExternalAccount.ConnectPassword(Guid.NewGuid(), "imap", "u", "ENC(pw)", null, Clock);

        account.Rename("  Personal  ");
        Assert.Equal("Personal", account.Label);

        account.Rename(new string('x', ExternalAccount.MaxLabelLength + 20));
        Assert.Equal(ExternalAccount.MaxLabelLength, account.Label.Length);
    }

    private sealed class FixedTime(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
