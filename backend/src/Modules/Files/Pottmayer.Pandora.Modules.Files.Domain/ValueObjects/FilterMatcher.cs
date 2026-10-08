using Pottmayer.Pandora.Shared.Domain;

namespace Pottmayer.Pandora.Modules.Files.Domain.ValueObjects;

/// <summary>How a filter's pattern is tested against a name (or, for a glob with a slash, the path).</summary>
public sealed record FilterMatcher : IDomainValue<FilterMatcher>
{
    public string Value { get; }

    private FilterMatcher(string value) => Value = value;

    public static readonly FilterMatcher Extension  = new("extension");
    public static readonly FilterMatcher Glob       = new("glob");
    public static readonly FilterMatcher StartsWith = new("starts-with");
    public static readonly FilterMatcher EndsWith   = new("ends-with");
    public static readonly FilterMatcher Contains   = new("contains");
    public static readonly FilterMatcher Regex      = new("regex");

    public static readonly IReadOnlyList<FilterMatcher> All = [Extension, Glob, StartsWith, EndsWith, Contains, Regex];

    public static bool IsSupported(string? value) => All.Any(x => x.Value == value);

    public static FilterMatcher FromValue(string value) =>
        All.FirstOrDefault(x => x.Value == value)
        ?? throw new ArgumentException($"Unsupported filter matcher value: '{value}'.", nameof(value));

    public override string ToString() => Value;
}
