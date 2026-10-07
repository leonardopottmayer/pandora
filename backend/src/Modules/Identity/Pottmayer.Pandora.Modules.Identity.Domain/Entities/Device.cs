using System.Text.RegularExpressions;
using Pottmayer.Pandora.Modules.Identity.Domain.ValueObjects;

namespace Pottmayer.Pandora.Modules.Identity.Domain.Entities;

/// <summary>
/// A paired client — Pandora Desktop, a headless agent, a phone — that calls the API with its own key
/// instead of the user's session. The key is limited to the device's <see cref="Scopes"/> and can be
/// revoked at any time. Only the SHA-256 of the key is persisted; the plaintext is shown once, at pairing.
/// </summary>
public sealed partial class Device
{
    public const int NameMaxLength = 100;

    /// <summary>How stale <see cref="LastSeenAt"/> may get before a request writes it again.</summary>
    private static readonly TimeSpan LastSeenResolution = TimeSpan.FromMinutes(5);

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public DevicePlatform Platform { get; private set; } = null!;
    public DeviceForm Form { get; private set; } = null!;
    public string KeyHash { get; private set; } = string.Empty;
    public string[] Scopes { get; private set; } = [];
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? LastSeenAt { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }

    private Device() { }

    /// <summary>Callers validate with <see cref="IsValidName"/> and <see cref="IsValidScope"/> first.</summary>
    public static Device Register(
        Guid userId, string name, DevicePlatform platform, DeviceForm form, string keyHash,
        IEnumerable<string> scopes, DateTimeOffset now)
        => new()
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            Name = name.Trim(),
            Platform = platform,
            Form = form,
            KeyHash = keyHash,
            Scopes = [.. scopes.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)],
            CreatedAt = now
        };

    public bool IsActive => RevokedAt is null;

    public void Revoke(DateTimeOffset now) => RevokedAt ??= now;

    /// <summary>Records a request from the device; returns false when the last record is recent enough to skip the write.</summary>
    public bool Touch(DateTimeOffset now)
    {
        if (LastSeenAt is { } last && now - last < LastSeenResolution) return false;
        LastSeenAt = now;
        return true;
    }

    public static bool IsValidName(string? name) =>
        !string.IsNullOrWhiteSpace(name) && name.Trim().Length <= NameMaxLength;

    /// <summary>Lower-case, dot-separated segments: <c>files.agent</c>.</summary>
    public static bool IsValidScope(string? scope) =>
        scope is { Length: > 0 and <= 64 } && ScopePattern().IsMatch(scope);

    [GeneratedRegex("^[a-z][a-z0-9-]*(\\.[a-z][a-z0-9-]*)*$")]
    private static partial Regex ScopePattern();
}
