using Microsoft.Extensions.DependencyInjection;
using Pottmayer.Pandora.Modules.Agenda.Application.Assistant;
using Pottmayer.Pandora.Modules.Assistant.Abstractions.Commands;
using Pottmayer.Tars.Core.Mediator.DI;
using Pottmayer.Tars.Messaging.DI;

namespace Pottmayer.Pandora.Modules.Agenda.Application.DI;

public static class ApplicationDI
{
    public static IServiceCollection AddAgendaApplication(this IServiceCollection services)
    {
        services.AddTarsMediator(opts =>
            opts.RegisterHandlersFromAssembly(typeof(ApplicationDI).Assembly));

        // Integration-event subscribers (dispatched by the in-process IIntegrationEventBus).
        services.AddIntegrationEventHandlersFromAssembly(typeof(ApplicationDI).Assembly);

        // The Agenda's contribution to the assistant tool catalog.
        services.AddScoped<IAssistantTool, CreateReminderTool>();

        return services;
    }
}
