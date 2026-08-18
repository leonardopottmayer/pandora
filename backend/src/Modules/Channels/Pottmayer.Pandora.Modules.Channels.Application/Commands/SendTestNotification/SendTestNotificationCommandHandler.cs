using Pottmayer.Pandora.Modules.Channels.Abstractions;
using Pottmayer.Pandora.Modules.Channels.Application.Enqueue;
using Pottmayer.Pandora.Modules.Channels.Domain.Errors;
using Pottmayer.Pandora.Modules.Channels.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Channels.Domain.ValueObjects;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Channels.Application.Commands.SendTestNotification;

/// <summary>
/// Queues a test message to the user's own address on one channel. The answer to "did my
/// notification actually go out?" starts by proving the channel works at all.
/// </summary>
public sealed class SendTestNotificationCommandHandler(
    IUnitOfWorkFactory factory,
    NotificationEnqueuer enqueuer)
    : CommandHandlerBase<SendTestNotificationCommand, bool>
{
    private static readonly TemplateKey Template = TemplateKey.Create("channel-test");

    protected override async Task<Result<bool>> HandleAsync(SendTestNotificationCommand request, CancellationToken ct)
    {
        var input = request.Input;

        if (!Channel.TryFromValue(input.Channel, out var channel))
            return Fail(ChannelErrors.UnsupportedChannel(input.Channel));

        var link = await factory.ExecuteAsync(ChannelsModule.Name, async (context, token) =>
        {
            var channels = context.AcquireRepository<IUserChannelRepository>();
            return await channels.FindAsync(input.UserId, channel, token);
        }, cancellationToken: ct);

        if (link is null)
            return Fail(ChannelErrors.NotLinked);

        if (!link.IsUsable)
            return Fail(ChannelErrors.NotUsable);

        // A fresh correlation id every time: a test send is meant to be repeatable, so it must not
        // be swallowed by the dedup guard.
        await enqueuer.EnqueueAsync(
            channel,
            link.Address.Value,
            Template,
            link.Locale,
            new Dictionary<string, string>(),
            Guid.CreateVersion7(),
            ct: ct);

        return Ok(true);
    }
}
