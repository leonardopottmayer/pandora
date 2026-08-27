using Pottmayer.Pandora.Modules.Channels.Abstractions;
using Pottmayer.Pandora.Modules.Channels.Domain.Errors;
using Pottmayer.Pandora.Modules.Channels.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Channels.Domain.ValueObjects;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Channels.Application.Commands.UnlinkChannel;

/// <summary>
/// Forgets an address. The row goes away rather than being disabled: the user asked to disconnect,
/// and a later handshake writes a fresh one.
/// </summary>
public sealed class UnlinkChannelCommandHandler(IUnitOfWorkFactory factory)
    : CommandHandlerBase<UnlinkChannelCommand, bool>
{
    protected override async Task<Result<bool>> HandleAsync(UnlinkChannelCommand request, CancellationToken ct)
    {
        var input = request.Input;

        if (!Channel.TryFromValue(input.Channel, out var channel))
            return Fail(ChannelErrors.UnsupportedChannel(input.Channel));

        var removed = await factory.ExecuteAsync(ChannelsModule.DatabaseKey, async (context, token) =>
        {
            var channels = context.AcquireRepository<IUserChannelRepository>();

            var link = await channels.FindAsync(input.UserId, channel, token);
            if (link is null)
                return false;

            await channels.RemoveAsync(link, token);
            return true;
        }, cancellationToken: ct);

        return removed ? Ok(true) : Fail(ChannelErrors.NotLinked);
    }
}
