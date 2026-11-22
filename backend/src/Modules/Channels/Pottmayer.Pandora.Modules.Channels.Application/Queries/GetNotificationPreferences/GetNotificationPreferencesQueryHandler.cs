using Pottmayer.Pandora.Modules.Channels.Abstractions;
using Pottmayer.Pandora.Modules.Channels.Application.Dtos;
using Pottmayer.Pandora.Modules.Channels.Domain.Ports.Repositories;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Channels.Application.Queries.GetNotificationPreferences;

public sealed class GetNotificationPreferencesQueryHandler(IUnitOfWorkFactory factory)
    : QueryHandlerBase<GetNotificationPreferencesQuery, IReadOnlyList<NotificationPreferenceDto>>
{
    protected override async Task<Result<IReadOnlyList<NotificationPreferenceDto>>> HandleAsync(
        GetNotificationPreferencesQuery request, CancellationToken cancellationToken)
    {
        var preferences = await factory.ExecuteAsync(ChannelsModule.DatabaseKey, async (context, ct) =>
        {
            var repo = context.AcquireRepository<INotificationPreferenceRepository>();
            return await repo.GetByUserAsync(request.Input.UserId, ct);
        }, cancellationToken: cancellationToken);

        IReadOnlyList<NotificationPreferenceDto> dtos =
            [.. preferences.Select(p => new NotificationPreferenceDto(p.Category, p.Channels))];

        return Ok(dtos);
    }
}
