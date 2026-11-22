using Pottmayer.Pandora.Modules.Channels.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Security.Authorization.Abstractions;
using Pottmayer.Pandora.Modules.Channels.Abstractions;

namespace Pottmayer.Pandora.Modules.Channels.Application.Queries.GetDeliveryHistory;

public sealed record GetDeliveryHistoryInput(
    Guid UserId,
    string? Status,
    string? Channel,
    string? Category,
    DateTimeOffset? From,
    DateTimeOffset? To,
    int Skip,
    int Take);

[RequiresPermission(ChannelsPermissions.Notifications.Read)]
public sealed class GetDeliveryHistoryQuery(GetDeliveryHistoryInput input)
    : QueryBase<GetDeliveryHistoryInput, IReadOnlyList<NotificationHistoryDto>>(input);
