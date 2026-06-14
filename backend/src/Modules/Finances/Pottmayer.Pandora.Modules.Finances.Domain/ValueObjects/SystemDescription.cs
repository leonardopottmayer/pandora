namespace Pottmayer.Pandora.Modules.Finances.Domain.ValueObjects;

/// <summary>
/// Language-neutral descriptor for a transaction whose text is defined by the system (not typed by
/// the user) — e.g. an opening balance or a statement payment. We persist the stable <see cref="Key"/>
/// plus <see cref="Args"/> and never the rendered sentence, so the display text can be localized at
/// read time and stays correct even if the user later changes language.
/// </summary>
public sealed class SystemDescription : IEquatable<SystemDescription>
{
    public string Key { get; }
    public IReadOnlyList<string> Args { get; }

    private SystemDescription(string key, IReadOnlyList<string> args)
    {
        Key = key;
        Args = args;
    }

    /// <summary>Opening balance entry created when an account is opened with a starting balance.</summary>
    public static SystemDescription OpeningBalance() => new("transaction.opening-balance", []);

    /// <summary>Payment of a card statement; <paramref name="referenceMonth"/> is the "yyyy-MM" cycle.</summary>
    public static SystemDescription StatementPayment(string referenceMonth) =>
        new("transaction.statement-payment", [referenceMonth]);

    /// <summary>Rehydration from persistence.</summary>
    public static SystemDescription Create(string key, IReadOnlyList<string>? args) =>
        new(key, args ?? []);

    public bool Equals(SystemDescription? other) =>
        other is not null && Key == other.Key && Args.SequenceEqual(other.Args);

    public override bool Equals(object? obj) => Equals(obj as SystemDescription);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Key);
        foreach (var arg in Args) hash.Add(arg);
        return hash.ToHashCode();
    }
}
