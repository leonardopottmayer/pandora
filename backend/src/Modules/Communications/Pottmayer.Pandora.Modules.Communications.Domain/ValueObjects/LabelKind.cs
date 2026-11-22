using Pottmayer.Pandora.Shared.Domain;

namespace Pottmayer.Pandora.Modules.Communications.Domain.ValueObjects;

/// <summary>Whether the provider owns a label (inbox, sent...) or the user made it.</summary>
public sealed record LabelKind : IDomainValue<LabelKind>
{
    public string Value { get; }

    private LabelKind(string value) => Value = value;

    public static readonly LabelKind System = new("system");
    public static readonly LabelKind User = new("user");

    public static readonly IReadOnlyList<LabelKind> All = [System, User];

    public static bool IsSupported(string? value) => All.Any(x => x.Value == value);

    public static LabelKind FromValue(string value) =>
        All.FirstOrDefault(x => x.Value == value)
        ?? throw new ArgumentException($"Unsupported label kind value: '{value}'.", nameof(value));

    public override string ToString() => Value;
}
