using Microsoft.Extensions.DependencyInjection;
using Pottmayer.Pandora.Modules.Communications.Application.Commands.ManageLabels;
using Pottmayer.Pandora.Modules.Communications.Application.Commands.SyncMailbox;
using Pottmayer.Pandora.Modules.Communications.Application.Integrations;
using Pottmayer.Pandora.Modules.Integrations.Abstractions.Ports;
using Pottmayer.Tars.Core.Mediator.DI;
using Pottmayer.Tars.Messaging.DI;

namespace Pottmayer.Pandora.Modules.Communications.Application.DI;

public static class ApplicationDI
{
    public static IServiceCollection AddCommunicationsApplication(this IServiceCollection services)
    {
        services.AddTarsMediator(opts =>
            opts.RegisterHandlersFromAssembly(typeof(ApplicationDI).Assembly));

        // Integration-event subscribers (dispatched by the in-process IIntegrationEventBus).
        services.AddIntegrationEventHandlersFromAssembly(typeof(ApplicationDI).Assembly);

        services.AddSingleton<MailboxGates>();
        services.AddScoped<MailboxChangesStep>();
        services.AddScoped<LabelOperation>();

        // "Used by" on the connections screen: the account each mailbox runs on.
        services.AddScoped<IExternalAccountUsageSource, CommunicationsAccountUsageSource>();

        return services;
    }
}
