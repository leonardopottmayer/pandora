using Pottmayer.Pandora.Modules.Channels.Abstractions;
using Pottmayer.Pandora.Modules.Channels.Application.Enqueue;
using Pottmayer.Pandora.Modules.Channels.Contracts;
using Pottmayer.Pandora.Modules.Channels.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Channels.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Channels.Domain.ValueObjects;
using Pottmayer.Pandora.Modules.Identity.Abstractions.Ports;
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
///
/// Quiet hours are the last gate before fan-out: if the user is inside their "do not disturb" window
/// (evaluated in their own time zone) and set it to suppress, the whole notification is dropped. This
/// path only carries opt-in categories; security notifications take the enqueuer directly and never
/// reach here, so they are never suppressed.
/// </remarks>
public sealed class NotifyUserRequestedHandler(
    IUnitOfWorkFactory factory,
    NotificationEnqueuer enqueuer,
    IUserPreferencesReader preferences,
    TimeProvider timeProvider)
    : IIntegrationEventHandler<NotifyUserRequested>
{
    private sealed record Target(Channel Channel, string Address, string Locale);

    private sealed record Resolution(List<Target> Targets, UserNotificationSetting? Setting);

    public async Task HandleAsync(NotifyUserRequested @event, CancellationToken cancellationToken = default)
    {
        var resolution = await ResolveAsync(@event, cancellationToken);
        if (resolution.Targets.Count == 0)
            return;

        if (await IsSuppressedByQuietHoursAsync(@event.UserId, resolution.Setting, cancellationToken))
            return;

        // One group id across the fan-out, so the rows read as a single notification.
        var groupId = Guid.CreateVersion7();
        var templateKey = TemplateKey.Create(@event.TemplateKey);

        foreach (var target in resolution.Targets)
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

    private Task<Resolution> ResolveAsync(NotifyUserRequested @event, CancellationToken ct) =>
        factory.ExecuteAsync(ChannelsModule.DatabaseKey, async (context, token) =>
        {
            var preferenceRepo = context.AcquireRepository<INotificationPreferenceRepository>();
            var userChannels = context.AcquireRepository<IUserChannelRepository>();
            var settings = context.AcquireRepository<IUserNotificationSettingRepository>();

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
                var preference = await preferenceRepo.FindAsync(@event.UserId, @event.Category, token);
                // No row means "no choice made": default to everything usable, so a notification is
                // never silently dropped for lack of a setting. An empty row is a deliberate mute.
                wanted = preference is not null ? preference.ResolvedChannels() : [.. usable.Keys];
            }

            var targets = wanted
                .Where(usable.ContainsKey)
                .Select(channel => new Target(channel, usable[channel].Address.Value, usable[channel].Locale))
                .ToList();

            // Only read the settings row when there is something to suppress.
            var setting = targets.Count > 0
                ? await settings.FindByUserAsync(@event.UserId, token)
                : null;

            return new Resolution(targets, setting);
        }, cancellationToken: ct);

    /// <summary>
    /// Whether the user's quiet hours are set to suppress right now. Resolves the user's IANA zone
    /// from Identity (UTC when unknown) so the wall-clock window is compared against their local time.
    /// </summary>
    private async Task<bool> IsSuppressedByQuietHoursAsync(
        Guid userId, UserNotificationSetting? setting, CancellationToken ct)
    {
        if (setting is null || !setting.QuietHoursEnabled)
            return false;

        var prefs = await preferences.GetAsync(userId, ct);
        var zone = ResolveZone(prefs?.TimeZone);
        var localNow = TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), zone);
        return setting.ShouldSuppress(TimeOnly.FromTimeSpan(localNow.TimeOfDay));
    }

    private static TimeZoneInfo ResolveZone(string? ianaId)
    {
        if (string.IsNullOrWhiteSpace(ianaId))
            return TimeZoneInfo.Utc;

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(ianaId);
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.Utc;
        }
        catch (InvalidTimeZoneException)
        {
            return TimeZoneInfo.Utc;
        }
    }

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
