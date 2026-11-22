using Pottmayer.Pandora.Modules.Communications.Domain.ValueObjects;

namespace Pottmayer.Pandora.Modules.Communications.Domain.Entities;

/// <summary>
/// A Gmail label or an IMAP folder of one mailbox. On IMAP a message is in exactly one folder
/// (<see cref="IsExclusive"/>).
/// </summary>
public sealed class MailLabel
{
    public const int NameMaxLength = 100;

    public Guid Id { get; private set; }
    public Guid MailboxId { get; private set; }

    /// <summary>The Gmail label id, or the IMAP folder's full path.</summary>
    public string ProviderLabelId { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;
    public LabelKind Kind { get; private set; } = LabelKind.User;
    public LabelRole? Role { get; private set; }
    public bool IsExclusive { get; private set; }

    private MailLabel() { }

    public static MailLabel Create(
        Guid mailboxId, string providerLabelId, string name, LabelKind kind, LabelRole? role, bool isExclusive)
        => new()
        {
            Id = Guid.CreateVersion7(),
            MailboxId = mailboxId,
            ProviderLabelId = providerLabelId,
            Name = name,
            Kind = kind,
            Role = role,
            IsExclusive = isExclusive
        };

    /// <summary>A Gmail label keeps its id across a rename; the name and role follow the provider.</summary>
    public void Update(string name, LabelKind kind, LabelRole? role, bool isExclusive)
    {
        Name = name;
        Kind = kind;
        Role = role;
        IsExclusive = isExclusive;
    }
}
