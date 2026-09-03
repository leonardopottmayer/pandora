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

        var account = ExternalAccount.ConnectApiKey(userId, "gemini", "ENC(key)", "••••1234", Clock);

        Assert.Equal(userId, account.UserId);
        Assert.Equal("gemini", account.Provider);
        Assert.Equal(AuthKind.ApiKey, account.AuthKind);
        Assert.Equal(ExternalAccount.ApiKeyAccountId, account.ProviderAccountId);
        Assert.Equal(string.Empty, account.Scopes);
        Assert.Equal("ENC(key)", account.AccessTokenEnc);
        Assert.Null(account.AccessTokenExpiresAt);
        Assert.Null(account.RefreshTokenEnc);
        Assert.Equal(AccountStatus.Connected, account.Status);
        Assert.Equal("••••1234", account.DisplayName);
        Assert.Equal(Now, account.ConnectedAt);
        Assert.Equal(Now, account.CreatedAt);
        Assert.NotEqual(Guid.Empty, account.Id);
    }

    [Fact]
    public void ReplaceApiKey_swaps_the_key_and_clears_a_previous_error()
    {
        var account = ExternalAccount.ConnectApiKey(Guid.NewGuid(), "gemini", "ENC(old)", "••••1111", Clock);
        account.MarkRevoked("was revoked");

        var later = new FixedTime(Now.AddHours(1));
        account.ReplaceApiKey("ENC(new)", "••••2222", later);

        Assert.Equal("ENC(new)", account.AccessTokenEnc);
        Assert.Equal("••••2222", account.DisplayName);
        Assert.Equal(AccountStatus.Connected, account.Status);
        Assert.Null(account.LastError);
        Assert.Equal(Now.AddHours(1), account.LastRefreshedAt);
    }

    private sealed class FixedTime(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
