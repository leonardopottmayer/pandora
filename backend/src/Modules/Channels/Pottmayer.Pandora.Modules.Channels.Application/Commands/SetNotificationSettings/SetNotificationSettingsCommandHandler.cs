using System.Globalization;
using Pottmayer.Pandora.Modules.Channels.Abstractions;
using Pottmayer.Pandora.Modules.Channels.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Channels.Domain.Errors;
using Pottmayer.Pandora.Modules.Channels.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Channels.Domain.ValueObjects;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Channels.Application.Commands.SetNotificationSettings;

/// <summary>
/// Upserts the user's cross-category settings row. Enabling quiet hours validates and parses the
/// window; disabling clears it. A missing row is created lazily on first write.
/// </summary>
public sealed class SetNotificationSettingsCommandHandler(IUnitOfWorkFactory factory, TimeProvider timeProvider)
    : CommandHandlerBase<SetNotificationSettingsCommand, bool>
{
    private const string TimeFormat = "HH:mm";

    protected override async Task<Result<bool>> HandleAsync(
        SetNotificationSettingsCommand request, CancellationToken ct)
    {
        var input = request.Input;

        TimeOnly start = default, end = default;
        QuietHoursBehaviour behaviour = QuietHoursBehaviour.Suppress;

        if (input.QuietHoursEnabled)
        {
            if (!TryParseTime(input.QuietHoursStart, out start) || !TryParseTime(input.QuietHoursEnd, out end) || start == end)
                return Fail(ChannelErrors.InvalidQuietHoursWindow);

            if (!QuietHoursBehaviour.TryFromValue(input.QuietHoursBehaviour, out behaviour))
                return Fail(ChannelErrors.InvalidQuietHoursBehaviour(input.QuietHoursBehaviour ?? "null"));
        }

        await factory.ExecuteAsync(ChannelsModule.DatabaseKey, async (context, token) =>
        {
            var repo = context.AcquireRepository<IUserNotificationSettingRepository>();
            var setting = await repo.FindByUserAsync(input.UserId, token);

            var isNew = setting is null;
            setting ??= UserNotificationSetting.Create(input.UserId, timeProvider);

            if (input.QuietHoursEnabled)
                setting.SetQuietHours(start, end, behaviour);
            else
                setting.ClearQuietHours();

            if (isNew)
                await repo.AddAsync(setting, token);
            else
                await repo.UpdateAsync(setting, token);

            return true;
        }, cancellationToken: ct);

        return Ok(true);
    }

    private static bool TryParseTime(string? value, out TimeOnly time) =>
        TimeOnly.TryParseExact(value, TimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out time);
}
