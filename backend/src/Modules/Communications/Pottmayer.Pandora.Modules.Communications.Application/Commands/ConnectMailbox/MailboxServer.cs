using Pottmayer.Pandora.Modules.Communications.Domain.ValueObjects;

namespace Pottmayer.Pandora.Modules.Communications.Application.Commands.ConnectMailbox;

internal static class MailboxServer
{
    /// <summary>All four fields, or none (null); anything in between, or a bad host or port, is invalid.</summary>
    public static MailServerSettings? From(string? imapHost, int? imapPort, string? smtpHost, int? smtpPort, out bool valid)
    {
        valid = true;
        if (imapHost is null && imapPort is null && smtpHost is null && smtpPort is null)
            return null;

        var server = new MailServerSettings(imapHost?.Trim() ?? "", imapPort ?? 0, smtpHost?.Trim() ?? "", smtpPort ?? 0);
        valid = server.IsValid;
        return valid ? server : null;
    }
}
