namespace Pottmayer.Pandora.Modules.Finances.Domain.ValueObjects;

/// <summary>
/// An amount in a single currency. Stored as NUMERIC(20,8) so it covers both 2-decimal fiat and
/// 8-decimal crypto. Arithmetic is only defined between equal currencies — mixing currencies
/// throws, keeping per-account balance a plain sum (used by the ledger in phase 04).
/// </summary>
public sealed record Money
{
    public decimal Amount { get; }
    public CurrencyCode Currency { get; }

    public Money(decimal amount, CurrencyCode currency)
    {
        Amount = amount;
        Currency = currency;
    }

    public static Money Zero(CurrencyCode currency) => new(0m, currency);

    public Money Add(Money other)
    {
        EnsureSameCurrency(other);
        return new Money(Amount + other.Amount, Currency);
    }

    public Money Subtract(Money other)
    {
        EnsureSameCurrency(other);
        return new Money(Amount - other.Amount, Currency);
    }

    private void EnsureSameCurrency(Money other)
    {
        if (Currency.Value != other.Currency.Value)
            throw new InvalidOperationException(
                $"Cannot operate on amounts in different currencies ({Currency} vs {other.Currency}).");
    }

    public override string ToString() => $"{Amount} {Currency}";
}
