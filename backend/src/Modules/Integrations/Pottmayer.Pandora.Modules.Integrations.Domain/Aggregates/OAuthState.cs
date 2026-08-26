using Pottmayer.Tars.Core.Ddd;

namespace Pottmayer.Pandora.Modules.Integrations.Domain.Aggregates;

/// <summary>
/// An in-flight authorization request. Carries the CSRF <c>state</c> and the encrypted PKCE
/// verifier, and remembers where to bounce the browser back to in the SPA. Single use, short lived —
/// the callback authenticates by consuming exactly the state it issued.
/// </summary>
public sealed class OAuthState : AggregateRoot<Guid>
{
    public Guid UserId { get; private set; }
    public string Provider { get; private set; } = null!;
    public string State { get; private set; } = null!;

    /// <summary>Protected PKCE code verifier: a credential for the duration of the flow.</summary>
    public string CodeVerifierEnc { get; private set; } = null!;

    /// <summary>Relative path in the SPA to redirect to after the callback completes.</summary>
    public string RedirectAfter { get; private set; } = null!;

    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? ConsumedAt { get; private set; }

    private OAuthState() { }

    public static OAuthState Issue(
        Guid userId,
        string provider,
        string state,
        string codeVerifierEnc,
        string redirectAfter,
        DateTimeOffset expiresAt)
    {
        return new OAuthState
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            Provider = provider,
            State = state,
            CodeVerifierEnc = codeVerifierEnc,
            RedirectAfter = redirectAfter,
            ExpiresAt = expiresAt
        };
    }

    public bool IsUsable(DateTimeOffset now) => ConsumedAt is null && now < ExpiresAt;

    public void Consume(TimeProvider timeProvider) => ConsumedAt = timeProvider.GetUtcNow();
}
