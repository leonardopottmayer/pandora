namespace Pottmayer.Pandora.Host.RateLimiting;

/// <summary>Request limits per window (bound from the <c>Pandora:RateLimiting</c> section).</summary>
public sealed class RateLimitingOptions
{
    public const string SectionName = "Pandora:RateLimiting";

    /// <summary>Off, every request goes through. The integration tests run with it off.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Every <c>/api</c> request, per user (or per IP before sign-in).</summary>
    public RateLimit Api { get; set; } = new() { PermitLimit = 300 };

    /// <summary>Sign-in, sign-up, password and MFA endpoints, per IP.</summary>
    public RateLimit Auth { get; set; } = new() { PermitLimit = 10 };

    /// <summary>The Assistant's commands, per user.</summary>
    public RateLimit Assistant { get; set; } = new() { PermitLimit = 20 };
}

/// <summary>A fixed window: <see cref="PermitLimit"/> requests every <see cref="WindowSeconds"/>.</summary>
public sealed class RateLimit
{
    public int PermitLimit { get; set; }
    public int WindowSeconds { get; set; } = 60;
}
