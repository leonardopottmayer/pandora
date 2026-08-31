using Pottmayer.Pandora.Modules.Channels.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Queries;

namespace Pottmayer.Pandora.Modules.Channels.Application.Queries.GetNotificationSettings;

public sealed record GetNotificationSettingsInput(Guid UserId);

public sealed class GetNotificationSettingsQuery(GetNotificationSettingsInput input)
    : QueryBase<GetNotificationSettingsInput, NotificationSettingsDto>(input);
