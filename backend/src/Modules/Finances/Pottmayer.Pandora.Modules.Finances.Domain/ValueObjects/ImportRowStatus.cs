using Pottmayer.Pandora.Shared.Domain;

namespace Pottmayer.Pandora.Modules.Finances.Domain.ValueObjects;

/// <summary>
/// Processing outcome of a single imported row, independent of its <see cref="DedupStatus"/>:
/// <see cref="Pending"/> until processed, then <see cref="SuggestionCreated"/>,
/// <see cref="Skipped"/> (e.g. confirmed duplicate) or <see cref="Error"/>.
/// </summary>
public sealed class ImportRowStatus : IDomainValue<ImportRowStatus>
{
    public static readonly ImportRowStatus Pending = new("pending");
    public static readonly ImportRowStatus SuggestionCreated = new("suggestion-created");
    public static readonly ImportRowStatus Skipped = new("skipped");
    public static readonly ImportRowStatus Error = new("error");

    private static readonly Dictionary<string, ImportRowStatus> All = new()
    {
        [Pending.Value] = Pending,
        [SuggestionCreated.Value] = SuggestionCreated,
        [Skipped.Value] = Skipped,
        [Error.Value] = Error
    };

    public string Value { get; }

    private ImportRowStatus(string value) => Value = value;

    public static bool IsSupported(string? value) => value is not null && All.ContainsKey(value);

    public static ImportRowStatus FromValue(string value) =>
        All.TryGetValue(value, out var status)
            ? status
            : throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown import row status.");

    public override string ToString() => Value;
}
