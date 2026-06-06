using Microsoft.Extensions.DependencyInjection;
using Pottmayer.Pandora.Modules.Notifications.Application.Enqueue;
using Pottmayer.Tars.Core.Mediator.DI;
using Pottmayer.Tars.Messaging.DI;

namespace Pottmayer.Pandora.Modules.Notifications.Application.DI;

public static class ApplicationDI
{
    public static IServiceCollection AddNotificationsApplication(this IServiceCollection services)
    {
        services.AddTarsMediator(opts =>
            opts.RegisterHandlersFromAssembly(typeof(ApplicationDI).Assembly));

        services.AddScoped<NotificationEnqueuer>();

        // Integration-event subscribers (dispatched by the in-process IIntegrationEventBus).
        services.AddIntegrationEventHandlersFromAssembly(typeof(ApplicationDI).Assembly);

        return services;
    }
}
