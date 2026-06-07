namespace Pottmayer.Pandora.Modules.Identity.Application.Dtos;

/// <summary>Recovery codes in plaintext, returned a single time when generated.</summary>
public sealed record RecoveryCodesDto(IReadOnlyList<string> RecoveryCodes);
