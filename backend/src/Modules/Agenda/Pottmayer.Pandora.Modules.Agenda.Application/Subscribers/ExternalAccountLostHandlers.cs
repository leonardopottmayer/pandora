using Pottmayer.Pandora.Modules.Agenda.Abstractions;
using Pottmayer.Pandora.Modules.Agenda.Application.Commands.UnlinkCalendar;
using Pottmayer.Pandora.Modules.Agenda.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Integrations.Contracts;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;
using Pottmayer.Tars.Messaging.Abstractions;

namespace Pottmayer.Pandora.Modules.Agenda.Application.Subscribers;

/// <summary>The Google grant was revoked: its calendars stop syncing until the user reconnects (their events stay).</summary>
public sealed class ExternalAccountRevokedHandler(IUnitOfWorkFactory factory)
    : IIntegrationEventHandler<ExternalAccountRevoked>
{
    public Task HandleAsync(ExternalAccountRevoked @event, CancellationToken cancellationToken = default) =>
        factory.ExecuteAsync(AgendaModule.DatabaseKey, async (context, token) =>
        {
            var bindings = context.AcquireRepository<ICalendarBindingRepository>();
            foreach (var binding in await bindings.GetByAccountAsync(@event.ExternalAccountId, token))
            {
                binding.MarkNeedsReconnect("The Google account's access was revoked.");
                await bindings.UpdateAsync(binding, token);
            }
        }, cancellationToken: cancellationToken);
}

/// <summary>The user disconnected the account: its calendars become ordinary local calendars, with their events.</summary>
public sealed class ExternalAccountDisconnectedHandler(IUnitOfWorkFactory factory)
    : IIntegrationEventHandler<ExternalAccountDisconnected>
{
    public Task HandleAsync(ExternalAccountDisconnected @event, CancellationToken cancellationToken = default) =>
        factory.ExecuteAsync(AgendaModule.DatabaseKey, async (context, token) =>
        {
            var bindings = context.AcquireRepository<ICalendarBindingRepository>();
            foreach (var binding in await bindings.GetByAccountAsync(@event.ExternalAccountId, token))
                await CalendarUnlinking.UnlinkAsync(context, binding, token);
        }, cancellationToken: cancellationToken);
}
