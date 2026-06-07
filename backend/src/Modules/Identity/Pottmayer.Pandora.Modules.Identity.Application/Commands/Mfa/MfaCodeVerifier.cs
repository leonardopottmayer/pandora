using Pottmayer.Pandora.Modules.Identity.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Identity.Domain.Ports.Services;
using RecoveryCodeFactory = Pottmayer.Pandora.Modules.Identity.Application.Security.RecoveryCodes;

namespace Pottmayer.Pandora.Modules.Identity.Application.Commands.Mfa;

/// <summary>
/// Verifies a second-factor code for a user: first as a TOTP against the (decrypted) secret, then as
/// a one-time recovery code. A matching recovery code is consumed. Shared by the challenge, disable
/// and regenerate flows. Must run inside the unit-of-work transaction (it mutates recovery codes).
/// </summary>
internal static class MfaCodeVerifier
{
    public static async Task<bool> VerifyAsync(
        Guid userId,
        string code,
        IMfaCredentialRepository credentials,
        IMfaRecoveryCodeRepository recoveryCodes,
        ITotpAuthenticator totp,
        ISecretProtector protector,
        DateTimeOffset now,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(code))
            return false;

        var credential = await credentials.FindByUserIdAsync(userId, ct);
        if (credential is { IsConfirmed: true }
            && totp.VerifyCode(protector.Unprotect(credential.SecretCipher), code))
            return true;

        // Fall back to a recovery code.
        var hash = RecoveryCodeFactory.Hash(code);
        var codes = await recoveryCodes.ListByUserIdAsync(userId, ct);
        var match = codes.FirstOrDefault(c => c.IsConsumable && c.CodeHash == hash);
        if (match is null)
            return false;

        match.Consume(now);
        await recoveryCodes.UpdateAsync(match, ct);
        return true;
    }
}
