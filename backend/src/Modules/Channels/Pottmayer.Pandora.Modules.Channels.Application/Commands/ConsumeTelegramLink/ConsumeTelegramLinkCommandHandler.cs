using System.Text.Json;
using Pottmayer.Pandora.Modules.Channels.Abstractions;
using Pottmayer.Pandora.Modules.Channels.Application.Linking;
using Pottmayer.Pandora.Modules.Channels.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Channels.Domain.Errors;
using Pottmayer.Pandora.Modules.Channels.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Channels.Domain.ValueObjects;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Channels.Application.Commands.ConsumeTelegramLink;

/// <summary>
/// Consumes a single-use link token and records the chat id as the user's verified Telegram address.
/// Re-running the handshake from another chat relinks; a spent or expired token is refused.
/// </summary>
public sealed class ConsumeTelegramLinkCommandHandler(IUnitOfWorkFactory factory, TimeProvider timeProvider)
    : CommandHandlerBase<ConsumeTelegramLinkCommand, Guid>
{
    protected override async Task<Result<Guid>> HandleAsync(ConsumeTelegramLinkCommand request, CancellationToken ct)
    {
        var input = request.Input;

        // The chat id always arrives numeric, straight from Telegram.
        var address = NotificationAddress.Create(Channel.Telegram, input.ChatId);
        var tokenHash = ChannelLinkTokens.Hash(input.TokenPlaintext);
        var metadata = JsonSerializer.Serialize(new { username = input.Username, firstName = input.FirstName });

        return await factory.ExecuteAsync(ChannelsModule.DatabaseKey, async (context, token) =>
        {
            var tokens = context.AcquireRepository<IChannelLinkTokenRepository>();
            var userChannels = context.AcquireRepository<IUserChannelRepository>();
            var now = timeProvider.GetUtcNow();

            var link = await tokens.FindByHashAsync(tokenHash, token);
            if (link is null || !link.IsUsable(now))
                return Fail(ChannelErrors.LinkTokenInvalid);

            link.Consume(timeProvider);
            await tokens.UpdateAsync(link, token);

            var existing = await userChannels.FindAsync(link.UserId, Channel.Telegram, token);
            if (existing is null)
            {
                var channel = UserChannel.LinkVerified(
                    link.UserId, Channel.Telegram, address, link.Locale, metadata, timeProvider);
                await userChannels.AddAsync(channel, token);
            }
            else
            {
                existing.Relink(address, link.Locale, metadata, timeProvider);
                await userChannels.UpdateAsync(existing, token);
            }

            return Ok(link.UserId);
        }, cancellationToken: ct);
    }
}
