using Pottmayer.Pandora.Shared.Domain;

namespace Pottmayer.Pandora.Modules.Files.Domain.ValueObjects;

/// <summary>Where a scan stands. Only a completed scan marks entries missing.</summary>
public sealed record ScanStatus : IDomainValue<ScanStatus>
{
    public string Value { get; }

    private ScanStatus(string value) => Value = value;

    public static readonly ScanStatus Running   = new("running");
    public static readonly ScanStatus Completed = new("completed");
    /// <summary>Discarded: nothing was marked missing.</summary>
    public static readonly ScanStatus Aborted   = new("aborted");
    /// <summary>Stopped by the safety brake; waits for the user to confirm or discard it.</summary>
    public static readonly ScanStatus Held      = new("held");

    public static readonly IReadOnlyList<ScanStatus> All = [Running, Completed, Aborted, Held];

    public static bool IsSupported(string? value) => All.Any(x => x.Value == value);

    public static ScanStatus FromValue(string value) =>
        All.FirstOrDefault(x => x.Value == value)
        ?? throw new ArgumentException($"Unsupported scan status value: '{value}'.", nameof(value));

    public override string ToString() => Value;
}
