using Pottmayer.Pandora.Shared.Domain;

namespace Pottmayer.Pandora.Modules.Communications.Domain.ValueObjects;

/// <summary>
/// <c>broken</c> points at a label that no longer exists and does not run. <c>read-only</c> is a provider filter holding
/// what the model does not (a forward, a size), which only the provider can edit.
/// </summary>
public sealed record RuleStatus : IDomainValue<RuleStatus>
{
    public string Value { get; }

    private RuleStatus(string value) => Value = value;

    public static readonly RuleStatus Ok = new("ok");
    public static readonly RuleStatus Broken = new("broken");
    public static readonly RuleStatus ReadOnly = new("read-only");

    public static readonly IReadOnlyList<RuleStatus> All = [Ok, Broken, ReadOnly];

    public static bool IsSupported(string? value) => All.Any(x => x.Value == value);

    public static RuleStatus FromValue(string value) =>
        All.FirstOrDefault(x => x.Value == value)
        ?? throw new ArgumentException($"Unsupported rule status value: '{value}'.", nameof(value));

    public override string ToString() => Value;
}
