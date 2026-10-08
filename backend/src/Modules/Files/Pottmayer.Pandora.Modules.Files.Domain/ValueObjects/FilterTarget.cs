using Pottmayer.Pandora.Shared.Domain;

namespace Pottmayer.Pandora.Modules.Files.Domain.ValueObjects;

/// <summary>What a filter tests: files, or folders (which it can only exclude, pruning everything below).</summary>
public sealed record FilterTarget : IDomainValue<FilterTarget>
{
    public string Value { get; }

    private FilterTarget(string value) => Value = value;

    public static readonly FilterTarget File   = new("file");
    public static readonly FilterTarget Folder = new("folder");

    public static readonly IReadOnlyList<FilterTarget> All = [File, Folder];

    public static bool IsSupported(string? value) => All.Any(x => x.Value == value);

    public static FilterTarget FromValue(string value) =>
        All.FirstOrDefault(x => x.Value == value)
        ?? throw new ArgumentException($"Unsupported filter target value: '{value}'.", nameof(value));

    public override string ToString() => Value;
}
