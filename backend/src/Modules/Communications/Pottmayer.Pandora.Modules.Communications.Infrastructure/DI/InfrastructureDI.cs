using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Pottmayer.Pandora.Modules.Communications.Abstractions;
using Pottmayer.Pandora.Modules.Communications.Infrastructure.Gmail;
using Pottmayer.Pandora.Modules.Communications.Infrastructure.Imap;
using Pottmayer.Pandora.Modules.Communications.Infrastructure.Jobs;

namespace Pottmayer.Pandora.Modules.Communications.Infrastructure.DI;

public static class InfrastructureDI
{
    /// <summary>
    /// Wires the Communications module's infrastructure: the options and the mail providers (Gmail,
    /// IMAP) and the sync job.
    /// </summary>
    public static IHostApplicationBuilder AddCommunicationsInfrastructure(this IHostApplicationBuilder builder)
    {
        builder.Services
            .AddOptions<CommunicationsOptions>()
            .Bind(builder.Configuration.GetSection(CommunicationsOptions.SectionName));

        builder.Services.AddCommunicationsGmailProvider();
        builder.Services.AddCommunicationsImapProvider();
        builder.Services.AddHostedService<MailboxSyncBackgroundService>();

        return builder;
    }
}
