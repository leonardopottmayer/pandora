using Pottmayer.Tars.Core.Cqrs.Commands;

namespace Pottmayer.Pandora.Modules.Channels.Application.Commands.UnlinkChannel;

public sealed record UnlinkChannelInput(Guid UserId, string Channel);

public sealed class UnlinkChannelCommand(UnlinkChannelInput input)
    : CommandBase<UnlinkChannelInput, bool>(input);
