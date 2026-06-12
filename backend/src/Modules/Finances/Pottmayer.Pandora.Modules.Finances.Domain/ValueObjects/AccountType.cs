using Pottmayer.Pandora.Shared.Domain;

namespace Pottmayer.Pandora.Modules.Finances.Domain.ValueObjects;

/// <summary>Kind of balance repository an <c>Account</c> represents.</summary>
public sealed class AccountType : IDomainValue<AccountType>
{
    public static readonly AccountType Cash = new("cash");
    public static readonly AccountType Checking = new("checking");
    public static readonly AccountType Savings = new("savings");
    public static readonly AccountType International = new("international");
    public static readonly AccountType Crypto = new("crypto");
    public static readonly AccountType Investment = new("investment");
    public static readonly AccountType Other = new("other");

    private static readonly Dictionary<string, AccountType> All = new()
    {
        [Cash.Value] = Cash,
        [Checking.Value] = Checking,
        [Savings.Value] = Savings,
        [International.Value] = International,
        [Crypto.Value] = Crypto,
        [Investment.Value] = Investment,
        [Other.Value] = Other
    };

    public string Value { get; }

    private AccountType(string value) => Value = value;

    public static bool IsSupported(string? value) => value is not null && All.ContainsKey(value);

    public static AccountType FromValue(string value) =>
        All.TryGetValue(value, out var type)
            ? type
            : throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown account type.");

    public override string ToString() => Value;
}
