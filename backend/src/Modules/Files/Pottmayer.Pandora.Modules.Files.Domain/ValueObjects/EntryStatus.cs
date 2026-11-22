using Pottmayer.Pandora.Shared.Domain;

namespace Pottmayer.Pandora.Modules.Files.Domain.ValueObjects;

/// <summary>Whether the last completed scan saw the entry. Missing and excluded entries wait in the review inbox.</summary>
public sealed record EntryStatus : IDomainValue<EntryStatus>
{
    public string Value { get; }

    private EntryStatus(string value) => Value = value;

    public static readonly EntryStatus Present  = new("present");
    /// <summary>A completed scan of a reachable root did not see it.</summary>
    public static readonly EntryStatus Missing  = new("missing");
    /// <summary>The current selection or filters leave it out.</summary>
    public static readonly EntryStatus Excluded = new("excluded");

    public static readonly IReadOnlyList<EntryStatus> All = [Present, Missing, Excluded];

    public static bool IsSupported(string? value) => All.Any(x => x.Value == value);

    public static EntryStatus FromValue(string value) =>
        All.FirstOrDefault(x => x.Value == value)
        ?? throw new ArgumentException($"Unsupported entry status value: '{value}'.", nameof(value));

    public override string ToString() => Value;
}
