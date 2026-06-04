using Pottmayer.Tars.Security.Identity.Abstractions.Results;
using Pottmayer.Tars.Security.Identity.Abstractions.Services;
using Pottmayer.Tars.Security.Identity.Abstractions.Token;

namespace Pottmayer.Pandora.Modules.Identity.Tests.Fakes;

internal sealed class FakeTokenIssuer : ITokenIssuer
{
    public AuthenticationResult? LastAuth { get; private set; }

    public ValueTask<IssuedTokenResult> IssueAsync(AuthenticationResult auth, CancellationToken ct = default)
    {
        LastAuth = auth;
        return ValueTask.FromResult(new IssuedTokenResult("access-token", "jti", 9999));
    }
}

internal sealed class FakeRefreshTokenService : IRefreshTokenService
{
    public int IssueCount { get; private set; }

    public RefreshTokenConsumeResult? NextConsume { get; set; }

    public ValueTask<RefreshTokenIssueResult> IssueAsync(
        string subject,
        IReadOnlyList<ClaimData> claims,
        IReadOnlyDictionary<string, object?>? metadata,
        CancellationToken ct = default)
    {
        IssueCount++;
        return ValueTask.FromResult(new RefreshTokenIssueResult(
            OpaqueToken: "refresh-token",
            Id:          "refresh-id",
            ExpiresAt:   DateTimeOffset.UtcNow.AddDays(7),
            Subject:     subject,
            Claims:      claims));
    }

    public ValueTask<RefreshTokenConsumeResult?> ConsumeAsync(string opaqueToken, CancellationToken ct = default)
        => ValueTask.FromResult(NextConsume);

    public ValueTask RevokeAsync(string opaqueToken, CancellationToken ct = default) => ValueTask.CompletedTask;
}
