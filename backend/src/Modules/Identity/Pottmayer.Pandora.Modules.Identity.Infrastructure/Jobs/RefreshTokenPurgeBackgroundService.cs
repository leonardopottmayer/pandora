using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Pottmayer.Pandora.Modules.Identity.Application.Commands.PurgeRefreshTokens;
using Pottmayer.Tars.Core.Mediator.Abstractions;

namespace Pottmayer.Pandora.Modules.Identity.Infrastructure.Jobs;

public sealed class RefreshTokenPurgeBackgroundService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(24);
    private static readonly TimeSpan ConsumedRetention = TimeSpan.FromDays(7);
    private static readonly TimeSpan ExpiredRetention = TimeSpan.FromDays(30);

    private readonly IServiceProvider _serviceProvider;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<RefreshTokenPurgeBackgroundService> _logger;

    public RefreshTokenPurgeBackgroundService(
        IServiceProvider serviceProvider,
        TimeProvider timeProvider,
        ILogger<RefreshTokenPurgeBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval, _timeProvider);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PurgeAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Refresh token purge failed.");
            }

            try
            {
                if (!await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
                    break;
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task PurgeAsync(CancellationToken ct)
    {
        await using var scope = _serviceProvider.CreateAsyncScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        var now = _timeProvider.GetUtcNow();
        var command = new PurgeRefreshTokensCommand(new PurgeRefreshTokensInput(
            ConsumedOlderThan: now - ConsumedRetention,
            ExpiredOlderThan: now - ExpiredRetention));

        var result = await sender.Send(command, ct).ConfigureAwait(false);

        if (result.IsSuccess && result.Value > 0)
            _logger.LogInformation("Purged {Count} old refresh token(s).", result.Value);
        else if (result.IsFailure)
            _logger.LogWarning(
                "Refresh token purge command failed: {Errors}.",
                string.Join("; ", result.Errors.Select(e => $"{e.Code}: {e.Message}")));
    }
}
