using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using MailKit.Net.Imap;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using Xunit;

namespace Pottmayer.Pandora.IntegrationTests.Modules.Communications;

/// <summary>
/// A real IMAP/SMTP server (GreenMail) for the IMAP adapter's tests. Plain ports, no CONDSTORE and no
/// QRESYNC — the fallback paths. Each test logs in as its own user, so tests do not see each other's mail.
/// </summary>
public sealed class GreenMailFixture : IAsyncLifetime
{
    public const string Password = "secret";
    public static readonly string[] Users =
        ["sync@example.com", "auth@example.com", "flags@example.com", "move@example.com", "trash@example.com", "folders@example.com", "scope@example.com",
         "send@example.com", "to@example.com", "to2@example.com", "to3@example.com", "cc@example.com", "bcc@example.com", "copy@example.com", "nosent@example.com", "badsend@example.com"];

    private const int SmtpPort = 3025;
    private const int ImapPort = 3143;

    private readonly IContainer _container = new ContainerBuilder()
        .WithImage("greenmail/standalone:2.1.0")
        .WithEnvironment("GREENMAIL_OPTS", string.Join(' ',
            "-Dgreenmail.setup.test.smtp",
            "-Dgreenmail.setup.test.imap",
            "-Dgreenmail.hostname=0.0.0.0",
            "-Dgreenmail.users.login=email",
            "-Dgreenmail.users=" + string.Join(',', Users.Select(u => $"{u.Split('@')[0]}:{Password}@{u.Split('@')[1]}"))))
        .WithPortBinding(SmtpPort, true)
        .WithPortBinding(ImapPort, true)
        .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(ImapPort))
        .Build();

    public string Host => _container.Hostname;
    public int MappedImapPort => _container.GetMappedPublicPort(ImapPort);
    public int MappedSmtpPort => _container.GetMappedPublicPort(SmtpPort);

    public Task InitializeAsync() => _container.StartAsync();
    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    /// <summary>Delivers a message over SMTP and returns its Message-ID.</summary>
    public async Task<string> DeliverAsync(string to, string subject, string? inReplyTo = null)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress("Ana", "ana@sender.test"));
        message.To.Add(MailboxAddress.Parse(to));
        message.Subject = subject;
        message.MessageId = MimeKit.Utils.MimeUtils.GenerateMessageId("sender.test");
        if (inReplyTo is not null)
        {
            message.InReplyTo = inReplyTo;
            message.References.Add(inReplyTo);
        }
        message.Body = new TextPart("plain") { Text = $"Body of {subject}" };

        using var smtp = new SmtpClient();
        await smtp.ConnectAsync(Host, MappedSmtpPort, SecureSocketOptions.None);
        await smtp.SendAsync(message);
        await smtp.DisconnectAsync(true);
        return message.MessageId;
    }

    /// <summary>Another mail client (the user's webmail) acting on the same mailbox.</summary>
    public async Task<ImapClient> ConnectAsync(string user)
    {
        var client = new ImapClient();
        await client.ConnectAsync(Host, MappedImapPort, SecureSocketOptions.None);
        await client.AuthenticateAsync(user, Password);
        return client;
    }
}
