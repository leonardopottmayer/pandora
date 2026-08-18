using Pottmayer.Tars.Core.Cqrs.Commands;

namespace Pottmayer.Pandora.Modules.Channels.Application.Commands.SendTestNotification;

public sealed record SendTestNotificationInput(Guid UserId, string Channel);

public sealed class SendTestNotificationCommand(SendTestNotificationInput input)
    : CommandBase<SendTestNotificationInput, bool>(input);
