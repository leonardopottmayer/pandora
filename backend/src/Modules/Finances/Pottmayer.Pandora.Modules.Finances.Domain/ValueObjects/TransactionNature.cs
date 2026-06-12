using Pottmayer.Pandora.Shared.Domain;

namespace Pottmayer.Pandora.Modules.Finances.Domain.ValueObjects;

/// <summary>Whether a category classifies money going out (<see cref="Expense"/>) or in (<see cref="Income"/>).</summary>
public sealed class TransactionNature : IDomainValue<TransactionNature>
{
    public static readonly TransactionNature Expense = new("expense");
    public static readonly TransactionNature Income = new("income");

    public string Value { get; }

    private TransactionNature(string value) => Value = value;

    public static bool IsSupported(string? value) => value is "expense" or "income";

    public static TransactionNature FromValue(string value) => value switch
    {
        "expense" => Expense,
        "income" => Income,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown transaction nature.")
    };

    public override string ToString() => Value;
}
