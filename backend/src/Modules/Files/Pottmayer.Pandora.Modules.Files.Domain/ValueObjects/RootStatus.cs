using Pottmayer.Pandora.Shared.Domain;

namespace Pottmayer.Pandora.Modules.Files.Domain.ValueObjects;

/// <summary>Whether a root is still scanned. A removed root keeps its entries, as excluded, until the user reviews them.</summary>
public sealed record RootStatus : IDomainValue<RootStatus>
{
    public string Value { get; }

    private RootStatus(string value) => Value = value;

    public static readonly RootStatus Active  = new("active");
    public static readonly RootStatus Removed = new("removed");

    public static readonly IReadOnlyList<RootStatus> All = [Active, Removed];

    public static bool IsSupported(string? value) => All.Any(x => x.Value == value);

    public static RootStatus FromValue(string value) =>
        All.FirstOrDefault(x => x.Value == value)
        ?? throw new ArgumentException($"Unsupported root status value: '{value}'.", nameof(value));

    public override string ToString() => Value;
}
