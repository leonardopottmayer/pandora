namespace Pottmayer.Pandora.Shared.Domain;

/// <summary>
/// Names of the rate limiting policies the Host declares, for controllers to put on their actions
/// with <c>[EnableRateLimiting]</c>. Every <c>/api</c> request also counts against the global limit.
/// </summary>
public static class RateLimitPolicies
{
    /// <summary>Anonymous and credential endpoints (sign-in, sign-up, password, MFA), per client IP.</summary>
    public const string Auth = "auth";

    /// <summary>Commands that cost money on every call (the Assistant's model), per user.</summary>
    public const string Assistant = "assistant";
}
