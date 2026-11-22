using System.Security.Cryptography;

namespace Pottmayer.Pandora.Modules.Files.Agent;

/// <summary>
/// SHA-256 of the first and last 64 KiB of a file (the whole file when it is at most 128 KiB), as
/// lower-case hex. Read in about 128 KiB per file, it is cheap enough for 20 TB and, together with the
/// size, strong enough to recognize the same file at another path. Not a full-content hash.
/// </summary>
public static class Fingerprint
{
    public const int ChunkSize = 64 * 1024;

    public static bool IsValid(string? fingerprint) =>
        fingerprint is { Length: 64 } && fingerprint.All(char.IsAsciiHexDigitLower);

    /// <param name="stream">A seekable stream positioned at the start of the file.</param>
    public static async Task<string> ComputeAsync(Stream stream, CancellationToken ct = default)
    {
        using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[ChunkSize];
        var length = stream.Length;

        if (length <= 2L * ChunkSize)
        {
            int read;
            while ((read = await stream.ReadAsync(buffer, ct)) > 0)
                sha.AppendData(buffer, 0, read);
        }
        else
        {
            await stream.ReadExactlyAsync(buffer, ct);
            sha.AppendData(buffer);
            stream.Seek(-ChunkSize, SeekOrigin.End);
            await stream.ReadExactlyAsync(buffer, ct);
            sha.AppendData(buffer);
        }

        return Convert.ToHexStringLower(sha.GetHashAndReset());
    }
}
