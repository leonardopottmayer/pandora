using Pottmayer.Pandora.Shared.Domain;

namespace Pottmayer.Pandora.Modules.Communications.Domain.ValueObjects;

/// <summary>Where a mailbox lives. <c>titan</c> is <c>imap</c> with Titan's hosts preset.</summary>
public sealed record MailProvider : IDomainValue<MailProvider>
{
    public string Value { get; }

    /// <summary>The server settings used when the user gives none; null when the user must give them.</summary>
    public MailServerSettings? Preset { get; }

    /// <summary>The Integrations provider whose accounts this mailbox runs on.</summary>
    public string AccountProvider { get; }

    /// <summary>Gmail talks to its API; the others to IMAP/SMTP servers.</summary>
    public bool UsesServerSettings => this != Gmail;

    private MailProvider(string value, string accountProvider, MailServerSettings? preset = null)
    {
        Value = value;
        AccountProvider = accountProvider;
        Preset = preset;
    }

    public static readonly MailProvider Gmail = new("gmail", "google");
    public static readonly MailProvider Titan = new("titan", "titan",
        new MailServerSettings("imap.titan.email", 993, "smtp.titan.email", 465));
    public static readonly MailProvider Imap = new("imap", "imap");

    public static readonly IReadOnlyList<MailProvider> All = [Gmail, Titan, Imap];

    public static bool IsSupported(string? value) => All.Any(x => x.Value == value);

    public static MailProvider FromValue(string value) =>
        All.FirstOrDefault(x => x.Value == value)
        ?? throw new ArgumentException($"Unsupported mail provider value: '{value}'.", nameof(value));

    public override string ToString() => Value;
}

/// <summary>
/// The IMAP and SMTP servers of a mailbox. The port says how the connection is secured (993/465 TLS,
/// 143/587 STARTTLS), as MailKit's <c>SecureSocketOptions.Auto</c> does.
/// </summary>
public sealed record MailServerSettings(string ImapHost, int ImapPort, string SmtpHost, int SmtpPort)
{
    public bool IsValid => IsHost(ImapHost) && IsPort(ImapPort) && IsHost(SmtpHost) && IsPort(SmtpPort);

    private static bool IsHost(string? host) =>
        !string.IsNullOrWhiteSpace(host) && host.Length <= Entities.Mailbox.HostMaxLength && !host.Any(char.IsWhiteSpace);

    private static bool IsPort(int port) => port is > 0 and <= 65535;
}
