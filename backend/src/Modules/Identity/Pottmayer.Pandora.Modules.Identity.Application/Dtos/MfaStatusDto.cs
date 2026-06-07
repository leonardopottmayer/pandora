namespace Pottmayer.Pandora.Modules.Identity.Application.Dtos;

/// <summary>MFA state for the account settings screen.</summary>
public sealed record MfaStatusDto(bool Enabled, int RemainingRecoveryCodes);
