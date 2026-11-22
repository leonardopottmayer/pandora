using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Security.Authorization.Abstractions;
using Pottmayer.Pandora.Modules.Channels.Abstractions;

namespace Pottmayer.Pandora.Modules.Channels.Application.Commands.SendTestNotification;

public sealed record SendTestNotificationInput(Guid UserId, string Channel);

[RequiresPermission(ChannelsPermissions.Channels.Test)]
public sealed class SendTestNotificationCommand(SendTestNotificationInput input)
    : CommandBase<SendTestNotificationInput, bool>(input);
