using Microsoft.Extensions.DependencyInjection;
using Pottmayer.Pandora.Modules.Communications.Domain.Ports;
using Pottmayer.Pandora.Modules.Communications.Domain.ValueObjects;

namespace Pottmayer.Pandora.Modules.Communications.Infrastructure.Imap;

public static class ImapProviderDI
{
    /// <summary>The IMAP adapter serves both <c>titan</c> (preset hosts) and <c>imap</c>.</summary>
    public static IServiceCollection AddCommunicationsImapProvider(this IServiceCollection services)
    {
        services.AddKeyedScoped<IMailboxProvider, ImapMailboxProvider>(MailProvider.Titan.Value);
        services.AddKeyedScoped<IMailboxProvider, ImapMailboxProvider>(MailProvider.Imap.Value);
        return services;
    }
}
