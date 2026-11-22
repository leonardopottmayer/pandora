using Microsoft.Extensions.DependencyInjection;
using Pottmayer.Pandora.Modules.Communications.Application.Commands.ManageLabels;
using Pottmayer.Pandora.Modules.Communications.Application.Commands.ManageRules;
using Pottmayer.Pandora.Modules.Communications.Application.Commands.SendMessage;
using Pottmayer.Pandora.Modules.Communications.Application.Commands.SyncMailbox;
using Pottmayer.Pandora.Modules.Communications.Application.Integrations;
using Pottmayer.Pandora.Modules.Integrations.Abstractions.Ports;
using Pottmayer.Tars.Core.Mediator.DI;
using Pottmayer.Tars.Messaging.DI;
using Pottmayer.Tars.FeatureFlags.DI;
using Pottmayer.Tars.Security.Authorization.DI;
using Pottmayer.Pandora.Modules.Communications.Abstractions;

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
        services.AddScoped<RuleOperation>();
        services.AddSingleton<SendKeys>();

        // "Used by" on the connections screen: the account each mailbox runs on.
        services.AddScoped<IExternalAccountUsageSource, CommunicationsAccountUsageSource>();

        // What an administrator can switch off and grant in this module (see the Admin module).
        services.AddTarsFeatureFlagDefinitions(f => f.Add(CommunicationsFeatures.Communications));
        services.AddTarsPermissionDefinitions(p => p
            .CheckUsagesIn(typeof(ApplicationDI).Assembly)
            .Module(CommunicationsPermissions.Module, feature: CommunicationsFeatures.Communications)
                .Resource(CommunicationsPermissions.Mailboxes.Resource).Crud()
                .Resource(CommunicationsPermissions.Messages.Resource).Read().Update().Actions(CommunicationsPermissions.Actions.Send)
                .Resource(CommunicationsPermissions.Labels.Resource).Read().Update()
                .Resource(CommunicationsPermissions.Rules.Resource).Read().Update());

        return services;
    }
}
