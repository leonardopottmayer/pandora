using Pottmayer.Pandora.Modules.Channels.Abstractions;
using Pottmayer.Pandora.Modules.Channels.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Channels.Domain.Errors;
using Pottmayer.Pandora.Modules.Channels.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Channels.Domain.ValueObjects;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Channels.Application.Commands.SetNotificationPreference;

/// <summary>
/// Upserts the user's channel choice for a category. An empty channel list is a valid, deliberate
/// mute; an unknown channel value is rejected.
/// </summary>
public sealed class SetNotificationPreferenceCommandHandler(IUnitOfWorkFactory factory, TimeProvider timeProvider)
    : CommandHandlerBase<SetNotificationPreferenceCommand, bool>
{
    protected override async Task<Result<bool>> HandleAsync(
        SetNotificationPreferenceCommand request, CancellationToken ct)
    {
        var input = request.Input;

        var channels = new List<Channel>();
        foreach (var value in input.Channels)
        {
            if (!Channel.TryFromValue(value, out var channel))
                return Fail(ChannelErrors.UnsupportedChannel(value));

            channels.Add(channel);
        }

        await factory.ExecuteAsync(ChannelsModule.DatabaseKey, async (context, token) =>
        {
            var repo = context.AcquireRepository<INotificationPreferenceRepository>();
            var existing = await repo.FindAsync(input.UserId, input.Category, token);

            if (existing is null)
            {
                var preference = NotificationPreference.Create(input.UserId, input.Category, channels, timeProvider);
                await repo.AddAsync(preference, token);
            }
            else
            {
                existing.SetChannels(channels);
                await repo.UpdateAsync(existing, token);
            }

            return true;
        }, cancellationToken: ct);

        return Ok(true);
    }
}
