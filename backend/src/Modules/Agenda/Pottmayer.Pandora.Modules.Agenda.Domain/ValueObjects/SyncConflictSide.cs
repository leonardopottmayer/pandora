using Pottmayer.Pandora.Shared.Domain;

namespace Pottmayer.Pandora.Modules.Agenda.Domain.ValueObjects;

/// <summary>The side whose edit lost a sync conflict: <c>pandora</c> or <c>google</c>.</summary>
public sealed record SyncConflictSide : IDomainValue<SyncConflictSide>
{
    public string Value { get; }

    private SyncConflictSide(string value) => Value = value;

    public static readonly SyncConflictSide Pandora = new("pandora");
    public static readonly SyncConflictSide Google = new("google");

    public static readonly IReadOnlyList<SyncConflictSide> All = [Pandora, Google];

    public static bool IsSupported(string? value) => All.Any(x => x.Value == value);

    public static SyncConflictSide FromValue(string value) =>
        All.FirstOrDefault(x => x.Value == value)
        ?? throw new ArgumentException($"Unsupported sync conflict side value: '{value}'.", nameof(value));

    public override string ToString() => Value;
}
