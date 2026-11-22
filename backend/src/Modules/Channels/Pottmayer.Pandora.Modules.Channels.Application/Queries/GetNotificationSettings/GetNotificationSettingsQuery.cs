using Pottmayer.Pandora.Modules.Channels.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Security.Authorization.Abstractions;
using Pottmayer.Pandora.Modules.Channels.Abstractions;

namespace Pottmayer.Pandora.Modules.Channels.Application.Queries.GetNotificationSettings;

public sealed record GetNotificationSettingsInput(Guid UserId);

[RequiresPermission(ChannelsPermissions.Notifications.Read)]
public sealed class GetNotificationSettingsQuery(GetNotificationSettingsInput input)
    : QueryBase<GetNotificationSettingsInput, NotificationSettingsDto>(input);
