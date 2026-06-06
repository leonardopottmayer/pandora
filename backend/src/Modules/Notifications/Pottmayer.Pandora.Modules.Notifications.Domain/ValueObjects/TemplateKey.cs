using Pottmayer.Pandora.Shared.Domain;

namespace Pottmayer.Pandora.Modules.Notifications.Domain.ValueObjects;

/// <summary>
/// Identifies a notification template (e.g. <c>account-activation</c>). The producer never knows the
/// template; subscribers map their integration event to a <see cref="TemplateKey"/>.
/// </summary>
public sealed record TemplateKey : IDomainValue<TemplateKey>
{
    public string Value { get; }

    private TemplateKey(string value) => Value = value;

    public static TemplateKey Create(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            throw new ArgumentException("Template key must not be empty.", nameof(raw));
        return new TemplateKey(raw.Trim().ToLowerInvariant());
    }

    public static TemplateKey FromValue(string value) => Create(value);

    public override string ToString() => Value;
}
