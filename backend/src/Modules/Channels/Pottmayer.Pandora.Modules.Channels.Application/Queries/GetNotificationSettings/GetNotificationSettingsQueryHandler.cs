using Pottmayer.Pandora.Modules.Channels.Abstractions;
using Pottmayer.Pandora.Modules.Channels.Application.Dtos;
using Pottmayer.Pandora.Modules.Channels.Domain.Ports.Repositories;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Channels.Application.Queries.GetNotificationSettings;

public sealed class GetNotificationSettingsQueryHandler(IUnitOfWorkFactory factory)
    : QueryHandlerBase<GetNotificationSettingsQuery, NotificationSettingsDto>
{
    private const string TimeFormat = "HH:mm";

    protected override async Task<Result<NotificationSettingsDto>> HandleAsync(
        GetNotificationSettingsQuery request, CancellationToken cancellationToken)
    {
        var setting = await factory.ExecuteAsync(ChannelsModule.DatabaseKey, async (context, ct) =>
        {
            var repo = context.AcquireRepository<IUserNotificationSettingRepository>();
            return await repo.FindByUserAsync(request.Input.UserId, ct);
        }, cancellationToken: cancellationToken);

        if (setting is null || !setting.QuietHoursEnabled)
            return Ok(new NotificationSettingsDto(false, null, null, null));

        return Ok(new NotificationSettingsDto(
            true,
            setting.QuietHoursStart!.Value.ToString(TimeFormat),
            setting.QuietHoursEnd!.Value.ToString(TimeFormat),
            setting.QuietHoursBehaviour!.Value));
    }
}
