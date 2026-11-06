using Pottmayer.Pandora.Modules.Channels.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Security.Authorization.Abstractions;
using Pottmayer.Pandora.Modules.Channels.Abstractions;

namespace Pottmayer.Pandora.Modules.Channels.Application.Queries.GetNotificationPreferences;

public sealed record GetNotificationPreferencesInput(Guid UserId);

[RequiresPermission(ChannelsPermissions.Notifications.Read)]
public sealed class GetNotificationPreferencesQuery(GetNotificationPreferencesInput input)
    : QueryBase<GetNotificationPreferencesInput, IReadOnlyList<NotificationPreferenceDto>>(input);
