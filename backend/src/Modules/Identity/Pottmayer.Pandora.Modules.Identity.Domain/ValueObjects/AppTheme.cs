using Pottmayer.Pandora.Shared.Domain;

namespace Pottmayer.Pandora.Modules.Identity.Domain.ValueObjects;

public sealed record AppTheme : IDomainValue<AppTheme>
{
    public string Value { get; }

    private AppTheme(string value) => Value = value;

    public static readonly AppTheme Light  = new("light");
    public static readonly AppTheme Dark   = new("dark");
    public static readonly AppTheme System = new("system");

    public static readonly IReadOnlyList<AppTheme> All = [Light, Dark, System];

    public static bool IsSupported(string value) => All.Any(t => t.Value == value);

    public static AppTheme FromValue(string value) =>
        All.FirstOrDefault(t => t.Value == value)
        ?? throw new ArgumentException($"Unsupported theme value: '{value}'.", nameof(value));

    public override string ToString() => Value;
}
