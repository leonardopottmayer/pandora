using Microsoft.Extensions.DependencyInjection;
using Pottmayer.Pandora.Modules.Channels.Application.Enqueue;
using Pottmayer.Tars.Core.Mediator.DI;
using Pottmayer.Tars.Messaging.DI;

namespace Pottmayer.Pandora.Modules.Channels.Application.DI;

public static class ApplicationDI
{
    public static IServiceCollection AddChannelsApplication(this IServiceCollection services)
    {
        services.AddTarsMediator(opts =>
            opts.RegisterHandlersFromAssembly(typeof(ApplicationDI).Assembly));

        services.AddScoped<NotificationEnqueuer>();

        // Integration-event subscribers (dispatched by the in-process IIntegrationEventBus).
        services.AddIntegrationEventHandlersFromAssembly(typeof(ApplicationDI).Assembly);

        return services;
    }
}
