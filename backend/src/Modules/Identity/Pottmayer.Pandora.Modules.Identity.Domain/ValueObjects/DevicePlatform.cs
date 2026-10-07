using Pottmayer.Pandora.Shared.Domain;

namespace Pottmayer.Pandora.Modules.Identity.Domain.ValueObjects;

/// <summary>The operating system a paired device runs on.</summary>
public sealed record DevicePlatform : IDomainValue<DevicePlatform>
{
    public string Value { get; }

    private DevicePlatform(string value) => Value = value;

    public static readonly DevicePlatform Windows = new("windows");
    public static readonly DevicePlatform Linux   = new("linux");
    public static readonly DevicePlatform MacOs   = new("macos");
    public static readonly DevicePlatform Android = new("android");
    public static readonly DevicePlatform Ios     = new("ios");

    public static readonly IReadOnlyList<DevicePlatform> All = [Windows, Linux, MacOs, Android, Ios];

    public static bool IsSupported(string? value) => All.Any(p => p.Value == value);

    public static DevicePlatform FromValue(string value) =>
        All.FirstOrDefault(p => p.Value == value)
        ?? throw new ArgumentException($"Unsupported device platform value: '{value}'.", nameof(value));

    public override string ToString() => Value;
}
