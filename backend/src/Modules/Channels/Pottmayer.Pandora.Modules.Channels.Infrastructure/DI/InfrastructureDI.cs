using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Pottmayer.Pandora.Modules.Channels.Abstractions;
using Pottmayer.Pandora.Modules.Channels.Domain.Ports.Services;
using Pottmayer.Pandora.Modules.Channels.Infrastructure.Ingress;
using Pottmayer.Pandora.Modules.Channels.Infrastructure.Jobs;
using Pottmayer.Pandora.Modules.Channels.Infrastructure.Templates;
using Pottmayer.Pandora.Modules.Channels.Infrastructure.Transports;
using Pottmayer.Tars.Communication.Email.DI;
using Pottmayer.Tars.Communication.Email.MailKit.DI;
using Pottmayer.Tars.Communication.Telegram.DI;
using Pottmayer.Tars.Communication.Telegram.Options;

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

        // Telegram transport (Tars.Communication.Telegram): registered only when a bot token is
        // present. Without it the channel stays dark instead of half-configured — a telegram send
        // would then find no transport and dead-letter, which is the honest outcome.
        if (!string.IsNullOrWhiteSpace(builder.Configuration[$"{TelegramOptions.SectionName}:BotToken"]))
        {
            builder.AddTarsTelegramOptions();
            builder.Services.AddTarsTelegramClient();
            builder.Services.AddScoped<IChannelTransport, TelegramChannelTransport>();

            // Inbound Telegram: the media reader and the triage the long-polling driver feeds.
            builder.Services.AddScoped<IInboundMediaReader, TelegramInboundMediaReader>();
            builder.Services.AddScoped<TelegramInboundTriage>();

            // The long-poll driver only actually pulls when LongPolling is on (checked inside), but it
            // needs a client to exist, which is why it lives under the bot-token guard.
            builder.Services.AddHostedService<TelegramLongPollingService>();
        }

        builder.Services.AddHostedService<NotificationDispatcherBackgroundService>();

        // Clears aged-out raw inbound payloads (chn004.raw). Runs regardless of the ingress driver;
        // gated internally by Channels:RawRetention:Enabled.
        builder.Services.AddHostedService<InboundUpdateRetentionBackgroundService>();

        return builder;
    }
}
