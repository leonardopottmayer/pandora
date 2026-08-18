using Pottmayer.Pandora.Modules.Channels.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Commands;

namespace Pottmayer.Pandora.Modules.Channels.Application.Commands.CreateChannelLink;

public sealed record CreateChannelLinkInput(Guid UserId, string Channel, string Locale);

public sealed class CreateChannelLinkCommand(CreateChannelLinkInput input)
    : CommandBase<CreateChannelLinkInput, ChannelLinkDto>(input);
