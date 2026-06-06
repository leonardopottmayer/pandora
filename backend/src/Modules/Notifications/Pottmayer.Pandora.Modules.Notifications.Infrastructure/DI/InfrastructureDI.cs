using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Pottmayer.Pandora.Modules.Notifications.Domain.Ports.Services;
using Pottmayer.Pandora.Modules.Notifications.Infrastructure.Jobs;
using Pottmayer.Pandora.Modules.Notifications.Infrastructure.Templates;
using Pottmayer.Tars.Communication.Email.DI;
using Pottmayer.Tars.Communication.Email.MailKit.DI;

namespace Pottmayer.Pandora.Modules.Notifications.Infrastructure.DI;

public static class InfrastructureDI
{
    public static IHostApplicationBuilder AddNotificationsInfrastructure(this IHostApplicationBuilder builder)
    {
        builder.Services
            .AddOptions<NotificationsOptions>()
            .Bind(builder.Configuration.GetSection(NotificationsOptions.SectionName));

        builder.Services.AddScoped<INotificationTemplateRenderer, InMemoryNotificationTemplateRenderer>();

        // E-mail transport (Tars.Communication): selected by config (Communication:Email:Provider).
        // "logging" (default) writes to the log; "mailkit" delivers over SMTP (e.g. Mailpit locally).
        var provider = builder.Configuration["Communication:Email:Provider"];
        if (string.Equals(provider, "mailkit", StringComparison.OrdinalIgnoreCase))
        {
            builder.AddTarsMailKitEmailOptions();
            builder.Services.AddTarsMailKitEmailSender();
        }
        else
        {
            builder.Services.AddTarsLoggingEmailSender();
        }

        builder.Services.AddHostedService<NotificationDispatcherBackgroundService>();

        return builder;
    }
}
