using Microsoft.Extensions.DependencyInjection;
using Pottmayer.Pandora.Modules.Communications.Domain.Ports;
using Pottmayer.Pandora.Modules.Communications.Domain.ValueObjects;

namespace Pottmayer.Pandora.Modules.Communications.Infrastructure.Gmail;

public static class GmailProviderDI
{
    public static IServiceCollection AddCommunicationsGmailProvider(this IServiceCollection services)
    {
        services.AddHttpClient<GmailMailboxProvider>();
        services.AddKeyedScoped<IMailboxProvider>(MailProvider.Gmail.Value,
            (sp, _) => sp.GetRequiredService<GmailMailboxProvider>());
        return services;
    }
}
