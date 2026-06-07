namespace Pottmayer.Pandora.Modules.Identity.Application.Dtos;

/// <summary>
/// Data returned when an MFA enrollment is started: the Base32 secret (manual entry) and the
/// <c>otpauth://</c> URI to render as a QR code.
/// </summary>
public sealed record MfaSetupDto(string Secret, string OtpauthUri);
