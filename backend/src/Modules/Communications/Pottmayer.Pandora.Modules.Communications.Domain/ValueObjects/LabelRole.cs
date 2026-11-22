using Pottmayer.Pandora.Shared.Domain;

namespace Pottmayer.Pandora.Modules.Communications.Domain.ValueObjects;

/// <summary>What a label is for, whatever its name in the provider.</summary>
public sealed record LabelRole : IDomainValue<LabelRole>
{
    public string Value { get; }

    private LabelRole(string value) => Value = value;

    public static readonly LabelRole Inbox = new("inbox");
    public static readonly LabelRole Sent = new("sent");
    public static readonly LabelRole Drafts = new("drafts");
    public static readonly LabelRole Trash = new("trash");
    public static readonly LabelRole Spam = new("spam");
    public static readonly LabelRole Archive = new("archive");

    public static readonly IReadOnlyList<LabelRole> All = [Inbox, Sent, Drafts, Trash, Spam, Archive];

    public static bool IsSupported(string? value) => All.Any(x => x.Value == value);

    public static LabelRole FromValue(string value) =>
        All.FirstOrDefault(x => x.Value == value)
        ?? throw new ArgumentException($"Unsupported label role value: '{value}'.", nameof(value));

    /// <summary>Gmail's system label ids. Gmail has no archive label: archived is "no <c>INBOX</c>".</summary>
    public static LabelRole? FromGmailLabelId(string labelId) => labelId switch
    {
        "INBOX" => Inbox,
        "SENT" => Sent,
        "DRAFT" => Drafts,
        "TRASH" => Trash,
        "SPAM" => Spam,
        _ => null
    };

    /// <summary>
    /// An IMAP folder: <c>INBOX</c> by name (case-insensitive, RFC 3501), the others by their SPECIAL-USE
    /// attributes (RFC 6154: <c>\Sent</c>, <c>\Drafts</c>, <c>\Trash</c>, <c>\Junk</c>, <c>\Archive</c>).
    /// </summary>
    public static LabelRole? FromImapFolder(string path, IEnumerable<string> specialUse)
    {
        if (string.Equals(path, "INBOX", StringComparison.OrdinalIgnoreCase))
            return Inbox;

        foreach (var attribute in specialUse)
        {
            var role = attribute.ToLowerInvariant() switch
            {
                @"\sent" => Sent,
                @"\drafts" => Drafts,
                @"\trash" => Trash,
                @"\junk" => Spam,
                @"\archive" => Archive,
                _ => null
            };
            if (role is not null)
                return role;
        }

        return null;
    }

    public override string ToString() => Value;
}
