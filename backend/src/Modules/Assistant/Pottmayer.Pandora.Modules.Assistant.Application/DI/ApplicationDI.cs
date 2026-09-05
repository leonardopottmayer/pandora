using Microsoft.Extensions.DependencyInjection;
using Pottmayer.Tars.Core.Mediator.DI;
using Pottmayer.Tars.Messaging.DI;

namespace Pottmayer.Pandora.Modules.Assistant.Application.DI;

public static class ApplicationDI
{
    public static IServiceCollection AddAssistantApplication(this IServiceCollection services)
    {
        services.AddTarsMediator(opts =>
            opts.RegisterHandlersFromAssembly(typeof(ApplicationDI).Assembly));

        // Integration-event subscribers (e.g. inbound Telegram messages from Channels).
        services.AddIntegrationEventHandlersFromAssembly(typeof(ApplicationDI).Assembly);

        return services;
    }
}
