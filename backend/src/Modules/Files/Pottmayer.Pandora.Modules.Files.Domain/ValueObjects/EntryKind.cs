using Pottmayer.Pandora.Shared.Domain;

namespace Pottmayer.Pandora.Modules.Files.Domain.ValueObjects;

/// <summary>A cataloged file or folder.</summary>
public sealed record EntryKind : IDomainValue<EntryKind>
{
    public string Value { get; }

    private EntryKind(string value) => Value = value;

    public static readonly EntryKind File      = new("file");
    public static readonly EntryKind Directory = new("directory");

    public static readonly IReadOnlyList<EntryKind> All = [File, Directory];

    public static bool IsSupported(string? value) => All.Any(x => x.Value == value);

    public static EntryKind FromValue(string value) =>
        All.FirstOrDefault(x => x.Value == value)
        ?? throw new ArgumentException($"Unsupported entry kind value: '{value}'.", nameof(value));

    public override string ToString() => Value;
}
