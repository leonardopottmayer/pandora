using Pottmayer.Pandora.Shared.Domain;

namespace Pottmayer.Pandora.Modules.Identity.Domain.ValueObjects;

public sealed record AppLanguage : IDomainValue<AppLanguage>
{
    public string Value { get; }

    private AppLanguage(string value) => Value = value;

    public static readonly AppLanguage PtBr = new("pt-BR");
    public static readonly AppLanguage En   = new("en");

    public static readonly IReadOnlyList<AppLanguage> All = [PtBr, En];

    public static bool IsSupported(string value) => All.Any(l => l.Value == value);

    public static AppLanguage FromValue(string value) =>
        All.FirstOrDefault(l => l.Value == value)
        ?? throw new ArgumentException($"Unsupported language value: '{value}'.", nameof(value));

    public override string ToString() => Value;
}
