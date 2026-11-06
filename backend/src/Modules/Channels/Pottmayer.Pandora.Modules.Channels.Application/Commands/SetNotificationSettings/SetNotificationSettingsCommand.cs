using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Security.Authorization.Abstractions;
using Pottmayer.Pandora.Modules.Channels.Abstractions;

namespace Pottmayer.Pandora.Modules.Channels.Application.Commands.SetNotificationSettings;

/// <summary>
/// Sets the user's cross-category delivery settings. When <paramref name="QuietHoursEnabled"/> is
/// false the window is cleared and the other fields are ignored. Times are "HH:mm" wall-clock in the
/// user's own time zone.
/// </summary>
public sealed record SetNotificationSettingsInput(
    Guid UserId,
    bool QuietHoursEnabled,
    string? QuietHoursStart,
    string? QuietHoursEnd,
    string? QuietHoursBehaviour);

[RequiresPermission(ChannelsPermissions.Notifications.Update)]
public sealed class SetNotificationSettingsCommand(SetNotificationSettingsInput input)
    : CommandBase<SetNotificationSettingsInput, bool>(input);
