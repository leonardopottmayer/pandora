using System.Net.Mail;

namespace Pottmayer.Pandora.Shared.Domain.ValueObjects;

public sealed record Email : IDomainValue<Email>
{
    public string Value { get; }

    private Email(string value) => Value = value.Trim().ToLowerInvariant();

    public static bool TryCreate(string? raw, out Email? email)
    {
        email = null;
        if (string.IsNullOrWhiteSpace(raw)) return false;
        if (!IsValid(raw)) return false;
        email = new Email(raw);
        return true;
    }

    public static Email Create(string raw)
    {
        if (!TryCreate(raw, out var email))
            throw new ArgumentException($"'{raw}' is not a valid email address.", nameof(raw));
        return email!;
    }

    public static Email FromValue(string value) => Create(value);

    public static bool IsValid(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        try
        {
            var addr = new MailAddress(value);
            return addr.Address == value.Trim();
        }
        catch { return false; }
    }

    public override string ToString() => Value;
}
