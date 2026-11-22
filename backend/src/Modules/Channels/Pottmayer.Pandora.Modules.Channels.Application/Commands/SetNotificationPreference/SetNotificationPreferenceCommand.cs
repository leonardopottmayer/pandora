using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Security.Authorization.Abstractions;
using Pottmayer.Pandora.Modules.Channels.Abstractions;

namespace Pottmayer.Pandora.Modules.Channels.Application.Commands.SetNotificationPreference;

public sealed record SetNotificationPreferenceInput(Guid UserId, string Category, IReadOnlyList<string> Channels);

[RequiresPermission(ChannelsPermissions.Notifications.Update)]
public sealed class SetNotificationPreferenceCommand(SetNotificationPreferenceInput input)
    : CommandBase<SetNotificationPreferenceInput, bool>(input);
