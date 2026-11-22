namespace Pottmayer.Pandora.Modules.Identity.Application.Options;

/// <summary>
/// Whether a new account needs an administrator's approval before it can sign in (bound from the
/// <c>Pandora:Identity:AccountApproval</c> section). Off by default.
/// </summary>
public sealed class AccountApprovalOptions
{
    public const string SectionName = "Pandora:Identity:AccountApproval";

    /// <summary>When on, an account activated from now on waits for approval, unless pre-approved.</summary>
    public bool Required { get; set; }
}
