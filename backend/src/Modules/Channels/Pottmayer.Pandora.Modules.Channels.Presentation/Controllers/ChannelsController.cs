using System.Globalization;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pottmayer.Pandora.Modules.Channels.Application.Commands.CreateChannelLink;
using Pottmayer.Pandora.Modules.Channels.Application.Commands.SendTestNotification;
using Pottmayer.Pandora.Modules.Channels.Application.Commands.SetNotificationPreference;
using Pottmayer.Pandora.Modules.Channels.Application.Commands.SetNotificationSettings;
using Pottmayer.Pandora.Modules.Channels.Application.Commands.UnlinkChannel;
using Pottmayer.Pandora.Modules.Channels.Application.Queries.GetDeliveryHistory;
using Pottmayer.Pandora.Modules.Channels.Application.Queries.GetNotificationPreferences;
using Pottmayer.Pandora.Modules.Channels.Application.Queries.GetNotificationSettings;
using Pottmayer.Pandora.Modules.Channels.Application.Queries.GetUserChannels;
using Pottmayer.Pandora.Shared.Domain;
using Pottmayer.Tars.Core.Mediator.Abstractions;
using Pottmayer.Tars.UserContext.Abstractions.Context;
using Pottmayer.Tars.Web.Http.Abstractions;
using Pottmayer.Tars.Web.Http.AspNetCore.Extensions;

namespace Pottmayer.Pandora.Modules.Channels.Presentation.Controllers;

[ApiController]
[Authorize]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/channels")]
public sealed class ChannelsController(
    ISender sender,
    IHttpErrorMapper errorMapper,
    IUserContextAccessor<UserData> userContextAccessor) : ControllerBase
{
    /// <summary>The user's addresses, linked or disabled, for the settings screen.</summary>
    [HttpGet]
    public async Task<IActionResult> GetAsync(CancellationToken ct)
    {
        var query = new GetUserChannelsQuery(new GetUserChannelsInput(UserId));
        var result = await sender.Send(query, ct);
        return result.ToActionResult(errorMapper);
    }

    /// <summary>
    /// Starts the handshake: returns the deep link the user taps. The chat id arrives later, from
    /// Telegram, carrying the code this issued.
    /// </summary>
    [HttpPost("{channel}/link")]
    public async Task<IActionResult> LinkAsync(string channel, CancellationToken ct)
    {
        var command = new CreateChannelLinkCommand(
            new CreateChannelLinkInput(UserId, channel, CultureInfo.CurrentUICulture.Name));
        var result = await sender.Send(command, ct);
        return result.ToActionResult(errorMapper);
    }

    /// <summary>Forgets the address on this channel.</summary>
    [HttpDelete("{channel}/link")]
    public async Task<IActionResult> UnlinkAsync(string channel, CancellationToken ct)
    {
        var command = new UnlinkChannelCommand(new UnlinkChannelInput(UserId, channel));
        var result = await sender.Send(command, ct);
        return result.ToActionResult(errorMapper);
    }

    /// <summary>Queues a test message to the user's own address on this channel.</summary>
    [HttpPost("{channel}/test")]
    public async Task<IActionResult> TestAsync(string channel, CancellationToken ct)
    {
        var command = new SendTestNotificationCommand(new SendTestNotificationInput(UserId, channel));
        var result = await sender.Send(command, ct);
        return result.ToActionResult(errorMapper);
    }

    /// <summary>
    /// The user's delivery history, newest first, for the settings screen. Answers "did it actually
    /// go out?" — filterable by status, channel, category and date, paged with skip/take.
    /// </summary>
    [HttpGet("notifications")]
    public async Task<IActionResult> GetHistoryAsync(
        [FromQuery] string? status,
        [FromQuery] string? channel,
        [FromQuery] string? category,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 50,
        CancellationToken ct = default)
    {
        var query = new GetDeliveryHistoryQuery(
            new GetDeliveryHistoryInput(UserId, status, channel, category, from, to, skip, take));
        var result = await sender.Send(query, ct);
        return result.ToActionResult(errorMapper);
    }

    /// <summary>The user's channel choices per category, for the settings screen.</summary>
    [HttpGet("preferences")]
    public async Task<IActionResult> GetPreferencesAsync(CancellationToken ct)
    {
        var query = new GetNotificationPreferencesQuery(new GetNotificationPreferencesInput(UserId));
        var result = await sender.Send(query, ct);
        return result.ToActionResult(errorMapper);
    }

    /// <summary>
    /// Sets the channels a category goes out on. An empty list mutes the category; unknown channels
    /// are rejected.
    /// </summary>
    [HttpPut("preferences/{category}")]
    public async Task<IActionResult> SetPreferenceAsync(
        string category, [FromBody] SetPreferenceRequest body, CancellationToken ct)
    {
        var command = new SetNotificationPreferenceCommand(
            new SetNotificationPreferenceInput(UserId, category, body.Channels ?? []));
        var result = await sender.Send(command, ct);
        return result.ToActionResult(errorMapper);
    }

    /// <summary>The user's cross-category delivery settings (quiet hours), for the settings screen.</summary>
    [HttpGet("notification-settings")]
    public async Task<IActionResult> GetSettingsAsync(CancellationToken ct)
    {
        var query = new GetNotificationSettingsQuery(new GetNotificationSettingsInput(UserId));
        var result = await sender.Send(query, ct);
        return result.ToActionResult(errorMapper);
    }

    /// <summary>
    /// Sets the user's quiet hours. <c>quietHoursEnabled: false</c> clears the window; when true,
    /// <c>quietHoursStart</c>/<c>quietHoursEnd</c> are "HH:mm" wall-clock in the user's own time zone
    /// and <c>quietHoursBehaviour</c> is <c>suppress</c> or <c>deliver_anyway</c>.
    /// </summary>
    [HttpPut("notification-settings")]
    public async Task<IActionResult> SetSettingsAsync([FromBody] SetSettingsRequest body, CancellationToken ct)
    {
        var command = new SetNotificationSettingsCommand(new SetNotificationSettingsInput(
            UserId, body.QuietHoursEnabled, body.QuietHoursStart, body.QuietHoursEnd, body.QuietHoursBehaviour));
        var result = await sender.Send(command, ct);
        return result.ToActionResult(errorMapper);
    }

    private Guid UserId => userContextAccessor.Context.User!.Id;

    /// <summary>Body for setting a category's channels.</summary>
    public sealed record SetPreferenceRequest(IReadOnlyList<string>? Channels);

    /// <summary>Body for setting the user's quiet hours.</summary>
    public sealed record SetSettingsRequest(
        bool QuietHoursEnabled,
        string? QuietHoursStart,
        string? QuietHoursEnd,
        string? QuietHoursBehaviour);
}
