namespace Pottmayer.Pandora.Modules.Identity.Application.Options;

/// <summary>
/// Password reset settings (bound from the <c>Identity:PasswordReset</c> section).
/// </summary>
public sealed class PasswordResetOptions
{
    public const string SectionName = "Pandora:Identity:PasswordReset";

    /// <summary>How long a reset token stays valid after being requested.</summary>
    public TimeSpan TokenLifetime { get; set; } = TimeSpan.FromHours(1);
}
