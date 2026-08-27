using Pottmayer.Pandora.Modules.Channels.Abstractions;
using Pottmayer.Pandora.Modules.Channels.Application.Enqueue;
using Pottmayer.Pandora.Modules.Channels.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Channels.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Channels.Domain.ValueObjects;
using Pottmayer.Pandora.Modules.Identity.Contracts.IntegrationEvents;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;
using Pottmayer.Tars.Messaging.Abstractions;

namespace Pottmayer.Pandora.Modules.Channels.Application.Subscribers;

/// <summary>
/// Provisions the user's verified e-mail channel the moment Identity confirms the address. Activation
/// is the ownership proof a handshake would otherwise supply, so the address goes straight in as
/// <see cref="UserChannel.LinkVerified"/> — this is the "e-mail comes from the account itself" half
/// that <c>CreateChannelLink</c> deliberately leaves out. Without it a user has no usable e-mail
/// channel, so every preference-resolved notification (agenda alerts, reminders) silently finds no
/// e-mail target. Idempotent: an existing channel is relinked rather than duplicated.
/// </summary>
public sealed class AccountActivatedHandler(IUnitOfWorkFactory factory, TimeProvider timeProvider)
    : IIntegrationEventHandler<AccountActivated>
{
    public Task HandleAsync(AccountActivated @event, CancellationToken cancellationToken = default) =>
        factory.ExecuteAsync(ChannelsModule.Name, async (context, token) =>
        {
            var userChannels = context.AcquireRepository<IUserChannelRepository>();
            var address = NotificationAddress.Create(Channel.Email, @event.Email);
            var locale = Locale.Normalize(@event.Locale);

            var existing = await userChannels.FindAsync(@event.UserId, Channel.Email, token);
            if (existing is null)
            {
                var channel = UserChannel.LinkVerified(
                    @event.UserId, Channel.Email, address, locale, "{}", timeProvider);
                await userChannels.AddAsync(channel, token);
            }
            else
            {
                existing.Relink(address, locale, "{}", timeProvider);
                await userChannels.UpdateAsync(existing, token);
            }
        }, cancellationToken: cancellationToken);
}
