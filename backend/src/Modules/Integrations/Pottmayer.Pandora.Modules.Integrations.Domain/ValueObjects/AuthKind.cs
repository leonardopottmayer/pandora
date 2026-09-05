using Pottmayer.Pandora.Shared.Domain;

namespace Pottmayer.Pandora.Modules.Integrations.Domain.ValueObjects;

/// <summary>
/// How a connected account authenticates. Decides which columns are required and whether there is an
/// authorization flow at all.
/// </summary>
public sealed class AuthKind : IDomainValue<AuthKind>
{
    /// <summary>OAuth authorization-code flow with refreshable tokens (Google).</summary>
    public static readonly AuthKind OAuth = new("oauth");

    /// <summary>A user-supplied API key. No expiry, no refresh, no authorization flow (OpenAI, Gemini).</summary>
    public static readonly AuthKind ApiKey = new("api-key");

    public string Value { get; }

    private AuthKind(string value) => Value = value;

    public static AuthKind FromValue(string value) => value switch
    {
        "oauth" => OAuth,
        "api-key" => ApiKey,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown auth kind.")
    };

    public override string ToString() => Value;
}
