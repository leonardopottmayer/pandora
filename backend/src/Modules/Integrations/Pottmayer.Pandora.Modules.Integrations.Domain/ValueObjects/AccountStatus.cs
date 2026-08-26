using Pottmayer.Pandora.Shared.Domain;

namespace Pottmayer.Pandora.Modules.Integrations.Domain.ValueObjects;

/// <summary>Lifecycle of a connected account.</summary>
public sealed class AccountStatus : IDomainValue<AccountStatus>
{
    /// <summary>Usable: a valid credential can be obtained.</summary>
    public static readonly AccountStatus Connected = new("connected");

    /// <summary>The access token lapsed and there is no refresh token to renew it.</summary>
    public static readonly AccountStatus Expired = new("expired");

    /// <summary>The provider rejected the refresh (<c>invalid_grant</c>). Only reconnecting fixes it.</summary>
    public static readonly AccountStatus Revoked = new("revoked");

    /// <summary>Connected, but a newly requested feature needs scopes the user has not granted yet.</summary>
    public static readonly AccountStatus NeedsConsent = new("needs_consent");

    public string Value { get; }

    private AccountStatus(string value) => Value = value;

    public static AccountStatus FromValue(string value) => value switch
    {
        "connected" => Connected,
        "expired" => Expired,
        "revoked" => Revoked,
        "needs_consent" => NeedsConsent,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown account status.")
    };

    public override string ToString() => Value;
}
