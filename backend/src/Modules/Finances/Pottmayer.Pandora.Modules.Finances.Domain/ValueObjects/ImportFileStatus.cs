using Pottmayer.Pandora.Shared.Domain;

namespace Pottmayer.Pandora.Modules.Finances.Domain.ValueObjects;

/// <summary>
/// Lifecycle of an uploaded import file: <see cref="Received"/> → <see cref="Parsing"/> →
/// <see cref="Completed"/>, or <see cref="Failed"/> (retryable, goes back to <see cref="Received"/>)
/// or <see cref="Aborted"/> (terminal).
/// </summary>
public sealed class ImportFileStatus : IDomainValue<ImportFileStatus>
{
    public static readonly ImportFileStatus Received = new("received");
    public static readonly ImportFileStatus Parsing = new("parsing");
    public static readonly ImportFileStatus Completed = new("completed");
    public static readonly ImportFileStatus Failed = new("failed");
    public static readonly ImportFileStatus Aborted = new("aborted");

    private static readonly Dictionary<string, ImportFileStatus> All = new()
    {
        [Received.Value] = Received,
        [Parsing.Value] = Parsing,
        [Completed.Value] = Completed,
        [Failed.Value] = Failed,
        [Aborted.Value] = Aborted
    };

    public string Value { get; }

    private ImportFileStatus(string value) => Value = value;

    public static bool IsSupported(string? value) => value is not null && All.ContainsKey(value);

    public static ImportFileStatus FromValue(string value) =>
        All.TryGetValue(value, out var status)
            ? status
            : throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown import file status.");

    public override string ToString() => Value;
}
