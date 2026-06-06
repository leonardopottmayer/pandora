namespace Pottmayer.Pandora.Modules.Identity.Application.Options;

/// <summary>
/// Account activation settings (bound from the <c>Identity:AccountActivation</c> section).
/// </summary>
public sealed class AccountActivationOptions
{
    public const string SectionName = "Identity:AccountActivation";

    /// <summary>How long an activation token stays valid after sign-up.</summary>
    public TimeSpan TokenLifetime { get; set; } = TimeSpan.FromHours(24);
}
