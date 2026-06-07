namespace Pottmayer.Pandora.Modules.Identity.Application.Dtos;

/// <summary>
/// Outcome of a sign-in: either the issued <see cref="TokenDto"/> (no MFA) or an
/// <see cref="MfaChallengeDto"/> that must be completed with a second factor. Exactly one is set.
/// </summary>
public sealed record SignInResultDto(TokenDto? Tokens, MfaChallengeDto? Mfa);

public sealed record MfaChallengeDto(string Ticket, DateTimeOffset ExpiresAt);
