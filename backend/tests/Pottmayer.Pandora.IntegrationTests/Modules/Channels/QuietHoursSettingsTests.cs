using Microsoft.Extensions.DependencyInjection;
using Pottmayer.Pandora.IntegrationTests.Support;
using Pottmayer.Pandora.Modules.Channels.Application.Commands.SetNotificationSettings;
using Pottmayer.Pandora.Modules.Channels.Application.Queries.GetNotificationSettings;
using Pottmayer.Tars.Core.Mediator.Abstractions;
using Xunit;

namespace Pottmayer.Pandora.IntegrationTests.Modules.Channels;

/// <summary>
/// Round-trips the quiet-hours settings through a real database, proving the <c>chn007</c> migration
/// applied and the EF mapping (TimeOnly columns + the behaviour value-object conversion) persists and
/// reads back. The suppression logic itself is covered by unit tests.
/// </summary>
[Collection("Integration")]
public sealed class QuietHoursSettingsTests : IAsyncLifetime
{
    private readonly PandoraWebApplicationFactory _factory;

    public QuietHoursSettingsTests(PandoraWebApplicationFactory factory) => _factory = factory;

    public Task InitializeAsync() => _factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Setting_and_reading_quiet_hours_round_trips()
    {
        var userId = Guid.NewGuid();

        await SendAsync(new SetNotificationSettingsCommand(new SetNotificationSettingsInput(
            userId, QuietHoursEnabled: true, "22:00", "07:00", "suppress")));

        var settings = await QueryAsync(userId);

        Assert.True(settings.QuietHoursEnabled);
        Assert.Equal("22:00", settings.QuietHoursStart);
        Assert.Equal("07:00", settings.QuietHoursEnd);
        Assert.Equal("suppress", settings.QuietHoursBehaviour);
    }

    [Fact]
    public async Task Disabling_clears_the_window()
    {
        var userId = Guid.NewGuid();

        await SendAsync(new SetNotificationSettingsCommand(new SetNotificationSettingsInput(
            userId, QuietHoursEnabled: true, "22:00", "07:00", "deliver_anyway")));
        await SendAsync(new SetNotificationSettingsCommand(new SetNotificationSettingsInput(
            userId, QuietHoursEnabled: false, null, null, null)));

        var settings = await QueryAsync(userId);

        Assert.False(settings.QuietHoursEnabled);
        Assert.Null(settings.QuietHoursStart);
        Assert.Null(settings.QuietHoursEnd);
        Assert.Null(settings.QuietHoursBehaviour);
    }

    [Fact]
    public async Task An_invalid_window_is_rejected()
    {
        var userId = Guid.NewGuid();
        using var scope = _factory.Services.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        // Equal start and end is a zero-length window.
        var result = await sender.Send(new SetNotificationSettingsCommand(new SetNotificationSettingsInput(
            userId, QuietHoursEnabled: true, "09:00", "09:00", "suppress")), CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    private async Task SendAsync(SetNotificationSettingsCommand command)
    {
        using var scope = _factory.Services.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var result = await sender.Send(command, CancellationToken.None);
        Assert.True(result.IsSuccess, string.Join("; ", result.Errors.Select(e => e.Message)));
    }

    private async Task<GetNotificationSettingsResult> QueryAsync(Guid userId)
    {
        using var scope = _factory.Services.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var result = await sender.Send(
            new GetNotificationSettingsQuery(new GetNotificationSettingsInput(userId)), CancellationToken.None);
        Assert.True(result.IsSuccess);
        var dto = result.Value!;
        return new GetNotificationSettingsResult(
            dto.QuietHoursEnabled, dto.QuietHoursStart, dto.QuietHoursEnd, dto.QuietHoursBehaviour);
    }

    private sealed record GetNotificationSettingsResult(
        bool QuietHoursEnabled, string? QuietHoursStart, string? QuietHoursEnd, string? QuietHoursBehaviour);
}
