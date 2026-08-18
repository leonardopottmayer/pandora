using Microsoft.Extensions.Options;
using Pottmayer.Pandora.Modules.Channels.Abstractions;
using Pottmayer.Pandora.Modules.Channels.Application.Dtos;
using Pottmayer.Pandora.Modules.Channels.Application.Enqueue;
using Pottmayer.Pandora.Modules.Channels.Application.Linking;
using Pottmayer.Pandora.Modules.Channels.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Channels.Domain.Errors;
using Pottmayer.Pandora.Modules.Channels.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Channels.Domain.ValueObjects;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Channels.Application.Commands.CreateChannelLink;

/// <summary>
/// Issues a single-use code and returns the deep link that carries it. The chat id is never taken
/// from the client: it only ever arrives with the code, from Telegram itself.
/// </summary>
public sealed class CreateChannelLinkCommandHandler(
    IUnitOfWorkFactory factory,
    IOptions<ChannelsOptions> options,
    TimeProvider timeProvider)
    : CommandHandlerBase<CreateChannelLinkCommand, ChannelLinkDto>
{
    protected override async Task<Result<ChannelLinkDto>> HandleAsync(
        CreateChannelLinkCommand request, CancellationToken ct)
    {
        var input = request.Input;

        // Only Telegram is linked by a handshake; an e-mail address comes from the account itself.
        if (!string.Equals(input.Channel, Channel.Telegram.Value, StringComparison.OrdinalIgnoreCase))
            return Fail(ChannelErrors.LinkNotSupported(input.Channel));

        var botUsername = options.Value.Telegram.BotUsername;
        if (string.IsNullOrWhiteSpace(botUsername))
            return Fail(ChannelErrors.TelegramNotConfigured);

        var plaintext = ChannelLinkTokens.Generate();

        var issued = await factory.ExecuteAsync(ChannelsModule.Name, async (context, token) =>
        {
            var tokens = context.AcquireRepository<IChannelLinkTokenRepository>();

            var link = ChannelLinkToken.Issue(
                input.UserId, Channel.Telegram, ChannelLinkTokens.Hash(plaintext),
                Locale.Normalize(input.Locale), timeProvider);

            await tokens.AddAsync(link, token);
            return link;
        }, cancellationToken: ct);

        return Ok(new ChannelLinkDto($"https://t.me/{botUsername}?start={plaintext}", issued.ExpiresAt));
    }
}
