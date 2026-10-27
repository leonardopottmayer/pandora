using Microsoft.Extensions.DependencyInjection;
using Pottmayer.Pandora.Modules.Assistant.Application.Integrations;
using Pottmayer.Pandora.Modules.Assistant.Application.Interpret;
using Pottmayer.Pandora.Modules.Integrations.Abstractions.Ports;
using Pottmayer.Tars.Core.Mediator.DI;
using Pottmayer.Tars.Messaging.DI;

namespace Pottmayer.Pandora.Modules.Assistant.Application.DI;

public static class ApplicationDI
{
    public static IServiceCollection AddAssistantApplication(this IServiceCollection services)
    {
        services.AddTarsMediator(opts =>
            opts.RegisterHandlersFromAssembly(typeof(ApplicationDI).Assembly));

        // Locale + zone for confirmations and button taps (the pipeline builds its own).
        services.AddScoped<AssistantToolContextResolver>();

        // Tells Integrations which account the assistant uses, for the connections screen's "used by".
        services.AddScoped<IExternalAccountUsageSource, AssistantAccountUsageSource>();

        // Integration-event subscribers (e.g. inbound Telegram messages from Channels).
        services.AddIntegrationEventHandlersFromAssembly(typeof(ApplicationDI).Assembly);

        return services;
    }
}
