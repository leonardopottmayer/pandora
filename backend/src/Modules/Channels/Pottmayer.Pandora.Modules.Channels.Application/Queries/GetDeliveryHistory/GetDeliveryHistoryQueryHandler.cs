using Pottmayer.Pandora.Modules.Channels.Abstractions;
using Pottmayer.Pandora.Modules.Channels.Application.Dtos;
using Pottmayer.Pandora.Modules.Channels.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Channels.Domain.ValueObjects;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Channels.Application.Queries.GetDeliveryHistory;

/// <summary>
/// Reads a page of the user's delivery history. Unknown status/channel filters are ignored rather
/// than rejected — the screen builds them, so a stray value is a no-op, not a bad request.
/// </summary>
public sealed class GetDeliveryHistoryQueryHandler(IUnitOfWorkFactory factory)
    : QueryHandlerBase<GetDeliveryHistoryQuery, IReadOnlyList<NotificationHistoryDto>>
{
    private const int MaxTake = 200;

    protected override async Task<Result<IReadOnlyList<NotificationHistoryDto>>> HandleAsync(
        GetDeliveryHistoryQuery request, CancellationToken cancellationToken)
    {
        var input = request.Input;
        var take = input.Take is <= 0 or > MaxTake ? 50 : input.Take;
        var skip = input.Skip < 0 ? 0 : input.Skip;

        NotificationStatus? status =
            Enum.TryParse<NotificationStatus>(input.Status, ignoreCase: true, out var parsed) ? parsed : null;
        Channel? channel =
            input.Channel is not null && Channel.TryFromValue(input.Channel, out var c) ? c : null;

        var notifications = await factory.ExecuteAsync(ChannelsModule.Name, async (context, ct) =>
        {
            var repo = context.AcquireRepository<INotificationRepository>();
            return await repo.GetHistoryAsync(
                input.UserId, status, channel, input.Category, input.From, input.To, skip, take, ct);
        }, cancellationToken: cancellationToken);

        IReadOnlyList<NotificationHistoryDto> dtos = [.. notifications.Select(n => new NotificationHistoryDto(
            n.Id,
            n.Channel.Value,
            n.Category,
            n.TemplateKey.Value,
            n.Subject,
            n.Status.ToString(),
            n.AttemptCount,
            n.LastError,
            n.Provider,
            n.CorrelationId,
            n.GroupId,
            n.CreatedAt,
            n.UpdatedAt))];

        return Ok(dtos);
    }
}
