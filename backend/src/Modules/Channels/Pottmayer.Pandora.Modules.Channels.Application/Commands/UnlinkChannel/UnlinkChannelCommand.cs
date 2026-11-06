using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Security.Authorization.Abstractions;
using Pottmayer.Pandora.Modules.Channels.Abstractions;

namespace Pottmayer.Pandora.Modules.Channels.Application.Commands.UnlinkChannel;

public sealed record UnlinkChannelInput(Guid UserId, string Channel);

[RequiresPermission(ChannelsPermissions.Channels.Delete)]
public sealed class UnlinkChannelCommand(UnlinkChannelInput input)
    : CommandBase<UnlinkChannelInput, bool>(input);
