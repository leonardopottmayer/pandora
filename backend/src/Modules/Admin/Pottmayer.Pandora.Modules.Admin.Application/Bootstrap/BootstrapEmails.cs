using Pottmayer.Pandora.Modules.Admin.Abstractions;

namespace Pottmayer.Pandora.Modules.Admin.Application.Bootstrap;

/// <summary>The configured bootstrap e-mails, without blanks, compared case-insensitively.</summary>
public static class BootstrapEmails
{
    public static IEnumerable<string> From(AdminOptions options) =>
        options.BootstrapEmails.Where(e => !string.IsNullOrWhiteSpace(e)).Select(e => e.Trim());

    public static bool Contains(AdminOptions options, string email) =>
        From(options).Contains(email.Trim(), StringComparer.OrdinalIgnoreCase);
}
