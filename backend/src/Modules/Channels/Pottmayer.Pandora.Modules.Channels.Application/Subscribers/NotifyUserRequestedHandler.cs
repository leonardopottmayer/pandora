using Pottmayer.Pandora.Modules.Channels.Abstractions;
using Pottmayer.Pandora.Modules.Channels.Application.Enqueue;
using Pottmayer.Pandora.Modules.Channels.Contracts;
using Pottmayer.Pandora.Modules.Channels.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Channels.Domain.ValueObjects;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;
using Pottmayer.Tars.Messaging.Abstractions;

namespace Pottmayer.Pandora.Modules.Channels.Application.Subscribers;

/// <summary>
/// The per-user delivery path. Resolves which channels a category goes out on — the event's explicit
/// override, else the user's saved preference, else every channel they have usable — and fans out
/// into one queued notification per channel, all sharing a group id.
/// </summary>
/// <remarks>
/// The channel decision is a join between the preference and the addresses the user actually has
/// verified and enabled, read in one unit of work. An address the user cannot receive on is dropped
/// silently: a preference for Telegram is moot until a chat is linked.
/// </remarks>
public sealed class NotifyUserRequestedHandler(
    IUnitOfWorkFactory factory,
    NotificationEnqueuer enqueuer,
    TimeProvider timeProvider)
    : IIntegrationEventHandler<NotifyUserRequested>
{
    private sealed record Target(Channel Channel, string Address, string Locale);

    public async Task HandleAsync(NotifyUserRequested @event, CancellationToken cancellationToken = default)
    {
        var targets = await ResolveTargetsAsync(@event, cancellationToken);
        if (targets.Count == 0)
            return;

        // One group id across the fan-out, so the rows read as a single notification.
        var groupId = Guid.CreateVersion7();
        var templateKey = TemplateKey.Create(@event.TemplateKey);

        foreach (var target in targets)
        {
            await enqueuer.EnqueueAsync(
                target.Channel,
                target.Address,
                templateKey,
                Locale.Normalize(@event.Locale ?? target.Locale),
                @event.Payload,
                @event.CorrelationId,
                groupId: groupId,
                userId: @event.UserId,
                category: @event.Category,
                buttons: @event.Buttons,
                ct: cancellationToken);
        }
    }

    private Task<List<Target>> ResolveTargetsAsync(NotifyUserRequested @event, CancellationToken ct) =>
        factory.ExecuteAsync(ChannelsModule.Name, async (context, token) =>
        {
            var preferences = context.AcquireRepository<INotificationPreferenceRepository>();
            var userChannels = context.AcquireRepository<IUserChannelRepository>();

            // The addresses the user can actually receive on, keyed by channel.
            var usable = (await userChannels.GetByUserAsync(@event.UserId, token))
                .Where(c => c.IsUsable)
                .ToDictionary(c => c.Channel);

            IReadOnlyList<Channel> wanted;
            if (@event.Channels is not null)
            {
                // An explicit override wins over any saved preference — including an empty list, which
                // means "send nowhere".
                wanted = ParseChannels(@event.Channels);
            }
            else
            {
                var preference = await preferences.FindAsync(@event.UserId, @event.Category, token);
                // No row means "no choice made": default to everything usable, so a notification is
                // never silently dropped for lack of a setting. An empty row is a deliberate mute.
                wanted = preference is not null ? preference.ResolvedChannels() : [.. usable.Keys];
            }

            return wanted
                .Where(usable.ContainsKey)
                .Select(channel => new Target(channel, usable[channel].Address.Value, usable[channel].Locale))
                .ToList();
        }, cancellationToken: ct);

    private static IReadOnlyList<Channel> ParseChannels(IReadOnlyList<string> values)
    {
        var channels = new List<Channel>();
        foreach (var value in values)
        {
            if (Channel.TryFromValue(value, out var channel) && !channels.Contains(channel))
                channels.Add(channel);
        }

        return channels;
    }
}
