using Pottmayer.Pandora.Shared.Domain;

namespace Pottmayer.Pandora.Modules.Files.Domain.ValueObjects;

/// <summary>A selection mark: the folder and what lies below it are in or out, down to a deeper mark.</summary>
public sealed record SelectionMode : IDomainValue<SelectionMode>
{
    public string Value { get; }

    private SelectionMode(string value) => Value = value;

    public static readonly SelectionMode Include = new("include");
    public static readonly SelectionMode Exclude = new("exclude");

    public static readonly IReadOnlyList<SelectionMode> All = [Include, Exclude];

    public static bool IsSupported(string? value) => All.Any(x => x.Value == value);

    public static SelectionMode FromValue(string value) =>
        All.FirstOrDefault(x => x.Value == value)
        ?? throw new ArgumentException($"Unsupported selection mode value: '{value}'.", nameof(value));

    public override string ToString() => Value;
}
