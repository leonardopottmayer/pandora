using Pottmayer.Pandora.Modules.Agenda.Abstractions;
using Pottmayer.Pandora.Modules.Channels.Abstractions;
using Pottmayer.Pandora.Modules.Channels.Contracts;
using Pottmayer.Pandora.Modules.Identity.Abstractions;
using Pottmayer.Pandora.Modules.Identity.Contracts.IntegrationEvents;
using Pottmayer.Pandora.Modules.Integrations.Abstractions;
using Pottmayer.Pandora.Modules.Integrations.Contracts;
using Pottmayer.Tars.Messaging.Broker.DI;
using Pottmayer.Tars.Messaging.EntityFrameworkCore.DI;

namespace Pottmayer.Pandora.Host;

/// <summary>
/// The monolith's messaging transport, wired in one place. It is the <b>in-process</b> transactional
/// outbox: publishing writes a row in the producer's transaction, and a relay per producing database
/// drains it to the local handlers.
/// </summary>
/// <remarks>
/// Kept whole and at the host on purpose — this is the transport choice, and extracting a module into a
/// broker-backed service replaces this one method with the MassTransit registration
/// (<c>AddTarsMassTransitRabbitMq(o =&gt; o.UseEntityFrameworkOutbox(...))</c>). What survives the swap is
/// the contracts, the producers (<c>bus.PublishAsync</c> inside a unit of work) and the consumers
/// (<c>IIntegrationEventHandler&lt;T&gt;</c>); only the last-mile dispatcher is reused as-is.
/// </remarks>
internal static class OutboxRegistration
{
    public static IHostApplicationBuilder AddPandoraOutbox(this IHostApplicationBuilder builder)
    {
        var services = builder.Services;

        // Relay tuning bindable from Tars:Messaging:Outbox (defaults apply when the section is absent).
        builder.AddTarsOutboxOptions();

        // The process-wide registry — resolve a stored event name back to its type. One representative
        // event per contracts assembly is enough; the registry scans the whole assembly.
        services.AddTarsIntegrationEventTypeRegistry(
            typeof(AccountActivationRequested).Assembly,   // Identity.Contracts
            typeof(NotifyUserRequested).Assembly,          // Channels.Contracts
            typeof(ExternalAccountDisconnected).Assembly); // Integrations.Contracts

        // The last mile, the serializer, the outbox-backed bus and the outbox store.
        services.AddTarsIntegrationEventDispatcher();
        services.AddTarsIntegrationEventSerializer();
        services.AddTarsOutboxBus();
        services.AddTarsOutboxStore();

        // One relay per producing database (each hosts an outbox table mapped via AddTarsOutbox).
        services.AddTarsOutboxRelay(IdentityModule.DatabaseKey);
        services.AddTarsOutboxRelay(ChannelsModule.DatabaseKey);
        services.AddTarsOutboxRelay(AgendaModule.DatabaseKey);
        services.AddTarsOutboxRelay(IntegrationsModule.DatabaseKey);

        return builder;
    }
}
