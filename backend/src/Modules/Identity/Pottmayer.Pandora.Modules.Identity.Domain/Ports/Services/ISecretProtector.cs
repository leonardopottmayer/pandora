namespace Pottmayer.Pandora.Modules.Identity.Domain.Ports.Services;

/// <summary>
/// Symmetric, reversible protection for secrets that must be recovered later (e.g. the TOTP shared
/// secret). Unlike password/token hashing, the original value can be retrieved with the key.
/// </summary>
public interface ISecretProtector
{
    string Protect(string plaintext);
    string Unprotect(string cipher);
}
