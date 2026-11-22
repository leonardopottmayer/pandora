using Microsoft.Extensions.DependencyInjection;
using Pottmayer.Pandora.Modules.Channels.Application.Enqueue;
using Pottmayer.Tars.Core.Mediator.DI;
using Pottmayer.Tars.Messaging.DI;
using Pottmayer.Tars.FeatureFlags.DI;
using Pottmayer.Tars.Security.Authorization.DI;
using Pottmayer.Pandora.Modules.Channels.Abstractions;

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

        // What an administrator can switch off and grant in this module (see the Admin module).
        services.AddTarsFeatureFlagDefinitions(f => f.Add(ChannelsFeatures.Channels));
        services.AddTarsPermissionDefinitions(p => p
            .CheckUsagesIn(typeof(ApplicationDI).Assembly)
            .Module(ChannelsPermissions.Module, feature: ChannelsFeatures.Channels)
                .Resource(ChannelsPermissions.Channels.Resource).Read().Create().Delete().Actions(ChannelsPermissions.Actions.Test)
                .Resource(ChannelsPermissions.Notifications.Resource).Read().Update());

        return services;
    }
}
