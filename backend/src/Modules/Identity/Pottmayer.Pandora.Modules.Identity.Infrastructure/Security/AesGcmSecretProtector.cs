using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Pottmayer.Pandora.Modules.Identity.Application.Options;
using Pottmayer.Pandora.Modules.Identity.Domain.Ports.Services;

namespace Pottmayer.Pandora.Modules.Identity.Infrastructure.Security;

/// <summary>
/// <see cref="ISecretProtector"/> using AES-256-GCM with a key from configuration. The stored value is
/// Base64(nonce | tag | ciphertext); GCM provides confidentiality and integrity in one pass.
/// </summary>
internal sealed class AesGcmSecretProtector : ISecretProtector
{
    private const int NonceSize = 12; // AES-GCM standard nonce
    private const int TagSize = 16;   // 128-bit authentication tag

    private readonly byte[] _key;

    public AesGcmSecretProtector(IOptions<MfaOptions> options)
    {
        var key = options.Value.EncryptionKey;
        if (string.IsNullOrWhiteSpace(key))
            throw new InvalidOperationException(
                $"'{MfaOptions.SectionName}:{nameof(MfaOptions.EncryptionKey)}' is required to protect MFA secrets.");

        _key = Convert.FromBase64String(key);
        if (_key.Length != 32)
            throw new InvalidOperationException(
                $"'{MfaOptions.SectionName}:{nameof(MfaOptions.EncryptionKey)}' must be a Base64-encoded 256-bit (32-byte) key.");
    }

    public string Protect(string plaintext)
    {
        var plainBytes = Encoding.UTF8.GetBytes(plaintext);
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var cipher = new byte[plainBytes.Length];
        var tag = new byte[TagSize];

        using var aes = new AesGcm(_key, TagSize);
        aes.Encrypt(nonce, plainBytes, cipher, tag);

        var output = new byte[NonceSize + TagSize + cipher.Length];
        Buffer.BlockCopy(nonce, 0, output, 0, NonceSize);
        Buffer.BlockCopy(tag, 0, output, NonceSize, TagSize);
        Buffer.BlockCopy(cipher, 0, output, NonceSize + TagSize, cipher.Length);

        return Convert.ToBase64String(output);
    }

    public string Unprotect(string cipher)
    {
        var input = Convert.FromBase64String(cipher);

        var nonce = input.AsSpan(0, NonceSize);
        var tag = input.AsSpan(NonceSize, TagSize);
        var cipherBytes = input.AsSpan(NonceSize + TagSize);
        var plainBytes = new byte[cipherBytes.Length];

        using var aes = new AesGcm(_key, TagSize);
        aes.Decrypt(nonce, cipherBytes, tag, plainBytes);

        return Encoding.UTF8.GetString(plainBytes);
    }
}
