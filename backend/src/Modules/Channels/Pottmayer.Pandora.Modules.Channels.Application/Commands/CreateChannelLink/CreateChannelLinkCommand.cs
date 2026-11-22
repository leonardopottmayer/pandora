using Pottmayer.Pandora.Modules.Channels.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Security.Authorization.Abstractions;
using Pottmayer.Pandora.Modules.Channels.Abstractions;

namespace Pottmayer.Pandora.Modules.Channels.Application.Commands.CreateChannelLink;

public sealed record CreateChannelLinkInput(Guid UserId, string Channel, string Locale);

[RequiresPermission(ChannelsPermissions.Channels.Create)]
public sealed class CreateChannelLinkCommand(CreateChannelLinkInput input)
    : CommandBase<CreateChannelLinkInput, ChannelLinkDto>(input);
