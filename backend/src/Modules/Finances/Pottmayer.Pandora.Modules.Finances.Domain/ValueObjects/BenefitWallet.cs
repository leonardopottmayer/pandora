using Pottmayer.Pandora.Shared.Domain;

namespace Pottmayer.Pandora.Modules.Finances.Domain.ValueObjects;

/// <summary>
/// Which wallet a benefit account (<see cref="AccountType.Benefit"/>) holds: food (supermarkets), meal
/// (restaurants) or flexible (spendable anywhere the provider accepts).
/// </summary>
public sealed class BenefitWallet : IDomainValue<BenefitWallet>
{
    public static readonly BenefitWallet Food = new("food");
    public static readonly BenefitWallet Meal = new("meal");
    public static readonly BenefitWallet Flexible = new("flexible");

    private static readonly Dictionary<string, BenefitWallet> All = new()
    {
        [Food.Value] = Food,
        [Meal.Value] = Meal,
        [Flexible.Value] = Flexible
    };

    public string Value { get; }

    private BenefitWallet(string value) => Value = value;

    public static bool IsSupported(string? value) => value is not null && All.ContainsKey(value);

    public static BenefitWallet FromValue(string value) =>
        All.TryGetValue(value, out var wallet)
            ? wallet
            : throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown benefit wallet.");

    public override string ToString() => Value;
}
