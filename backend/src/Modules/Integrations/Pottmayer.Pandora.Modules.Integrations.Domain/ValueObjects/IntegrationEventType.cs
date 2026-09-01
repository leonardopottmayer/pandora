using Pottmayer.Pandora.Shared.Domain;

namespace Pottmayer.Pandora.Modules.Integrations.Domain.ValueObjects;

/// <summary>
/// A notable moment in a connection's life, appended to the <c>int003</c> event log. The log is the
/// answer to "why did sync stop three days ago": the failure kinds carry the reason, the lifecycle
/// kinds carry the change. Successful refreshes are deliberately absent — they happen hourly and
/// <c>int001.last_refreshed_at</c> already records the last one, so logging them would bury the signal.
/// </summary>
public sealed class IntegrationEventType : IDomainValue<IntegrationEventType>
{
    /// <summary>An account was connected for the first time.</summary>
    public static readonly IntegrationEventType Connected = new("connected");

    /// <summary>An existing account re-consented (a reconnect, possibly widening scopes).</summary>
    public static readonly IntegrationEventType Reconnected = new("reconnected");

    /// <summary>A refresh failed transiently; the credential is still usable and it will be retried.</summary>
    public static readonly IntegrationEventType RefreshFailed = new("refresh_failed");

    /// <summary>The access token lapsed and there was no refresh token to renew it.</summary>
    public static readonly IntegrationEventType Expired = new("expired");

    /// <summary>The provider rejected the refresh (<c>invalid_grant</c>). Only reconnecting fixes it.</summary>
    public static readonly IntegrationEventType Revoked = new("revoked");

    /// <summary>The user disconnected the account.</summary>
    public static readonly IntegrationEventType Disconnected = new("disconnected");

    public string Value { get; }

    private IntegrationEventType(string value) => Value = value;

    public static IntegrationEventType FromValue(string value) => value switch
    {
        "connected" => Connected,
        "reconnected" => Reconnected,
        "refresh_failed" => RefreshFailed,
        "expired" => Expired,
        "revoked" => Revoked,
        "disconnected" => Disconnected,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown integration event type.")
    };

    public override string ToString() => Value;
}
