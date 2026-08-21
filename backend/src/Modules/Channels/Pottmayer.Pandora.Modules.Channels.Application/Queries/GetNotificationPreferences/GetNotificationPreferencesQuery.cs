using Pottmayer.Pandora.Modules.Channels.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Queries;

namespace Pottmayer.Pandora.Modules.Channels.Application.Queries.GetNotificationPreferences;

public sealed record GetNotificationPreferencesInput(Guid UserId);

public sealed class GetNotificationPreferencesQuery(GetNotificationPreferencesInput input)
    : QueryBase<GetNotificationPreferencesInput, IReadOnlyList<NotificationPreferenceDto>>(input);
