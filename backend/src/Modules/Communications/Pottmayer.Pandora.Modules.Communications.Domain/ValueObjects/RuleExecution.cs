using Pottmayer.Pandora.Shared.Domain;

namespace Pottmayer.Pandora.Modules.Communications.Domain.ValueObjects;

/// <summary>Where a rule runs: as a native filter at the provider, or in Pandora's engine on mail that arrives.</summary>
public sealed record RuleExecution : IDomainValue<RuleExecution>
{
    public string Value { get; }

    private RuleExecution(string value) => Value = value;

    public static readonly RuleExecution ProviderFilter = new("provider-filter");
    public static readonly RuleExecution Pandora = new("pandora");

    public static readonly IReadOnlyList<RuleExecution> All = [ProviderFilter, Pandora];

    public static bool IsSupported(string? value) => All.Any(x => x.Value == value);

    public static RuleExecution FromValue(string value) =>
        All.FirstOrDefault(x => x.Value == value)
        ?? throw new ArgumentException($"Unsupported rule execution value: '{value}'.", nameof(value));

    public override string ToString() => Value;
}
