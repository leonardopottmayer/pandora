using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Pottmayer.Pandora.Modules.Channels.Abstractions;
using Pottmayer.Pandora.Modules.Channels.Domain.Ports.Services;
using Pottmayer.Pandora.Modules.Channels.Infrastructure.Jobs;
using Pottmayer.Pandora.Modules.Channels.Infrastructure.Templates;
using Pottmayer.Pandora.Modules.Channels.Infrastructure.Transports;
using Pottmayer.Tars.Communication.Email.DI;
using Pottmayer.Tars.Communication.Email.MailKit.DI;

namespace Pottmayer.Pandora.Modules.Channels.Infrastructure.DI;

public static class InfrastructureDI
{
    public static IHostApplicationBuilder AddChannelsInfrastructure(this IHostApplicationBuilder builder)
    {
        builder.Services
            .AddOptions<ChannelsOptions>()
            .Bind(builder.Configuration.GetSection(ChannelsOptions.SectionName));

        builder.Services.AddScoped<INotificationTemplateRenderer, FileNotificationTemplateRenderer>();
        builder.Services.AddHostedService<TemplateCatalogValidator>();

        // E-mail transport (Tars.Communication): selected by config (Tars:Communication:Email:Provider).
        // "logging" (default) writes to the log; "mailkit" delivers over SMTP (e.g. Mailpit locally).
        var provider = builder.Configuration["Tars:Communication:Email:Provider"];
        if (string.Equals(provider, "mailkit", StringComparison.OrdinalIgnoreCase))
        {
            builder.AddTarsMailKitEmailOptions();
            builder.Services.AddTarsMailKitEmailSender();
        }
        else
        {
            builder.Services.AddTarsLoggingEmailSender();
        }

        // Channel transports. The dispatcher picks by Channel, so a new channel is a new registration
        // here and nothing else.
        builder.Services.AddScoped<IChannelTransport, EmailChannelTransport>();

        builder.Services.AddHostedService<NotificationDispatcherBackgroundService>();

        return builder;
    }
}
