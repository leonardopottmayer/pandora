using Pottmayer.Pandora.Shared.Domain;

namespace Pottmayer.Pandora.Modules.Identity.Domain.ValueObjects;

/// <summary>The set of colors the web client draws with. Light or dark is <see cref="AppTheme"/>'s to say.</summary>
public sealed record AppPalette : IDomainValue<AppPalette>
{
    public string Value { get; }

    private AppPalette(string value) => Value = value;

    public static readonly AppPalette Default = new("default");
    public static readonly AppPalette Claude  = new("claude");
    public static readonly AppPalette Reddit  = new("reddit");

    public static readonly IReadOnlyList<AppPalette> All = [Default, Claude, Reddit];

    public static bool IsSupported(string value) => All.Any(p => p.Value == value);

    public static AppPalette FromValue(string value) =>
        All.FirstOrDefault(p => p.Value == value)
        ?? throw new ArgumentException($"Unsupported palette value: '{value}'.", nameof(value));

    public override string ToString() => Value;
}
