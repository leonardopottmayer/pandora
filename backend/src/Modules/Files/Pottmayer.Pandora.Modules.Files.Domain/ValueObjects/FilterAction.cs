using Pottmayer.Pandora.Shared.Domain;

namespace Pottmayer.Pandora.Modules.Files.Domain.ValueObjects;

/// <summary>An include filter keeps only what matches; an exclude filter drops what matches, and always wins.</summary>
public sealed record FilterAction : IDomainValue<FilterAction>
{
    public string Value { get; }

    private FilterAction(string value) => Value = value;

    public static readonly FilterAction Include = new("include");
    public static readonly FilterAction Exclude = new("exclude");

    public static readonly IReadOnlyList<FilterAction> All = [Include, Exclude];

    public static bool IsSupported(string? value) => All.Any(x => x.Value == value);

    public static FilterAction FromValue(string value) =>
        All.FirstOrDefault(x => x.Value == value)
        ?? throw new ArgumentException($"Unsupported filter action value: '{value}'.", nameof(value));

    public override string ToString() => Value;
}
