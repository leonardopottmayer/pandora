using Pottmayer.Pandora.Modules.Channels.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Queries;

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

public sealed class GetDeliveryHistoryQuery(GetDeliveryHistoryInput input)
    : QueryBase<GetDeliveryHistoryInput, IReadOnlyList<NotificationHistoryDto>>(input);
