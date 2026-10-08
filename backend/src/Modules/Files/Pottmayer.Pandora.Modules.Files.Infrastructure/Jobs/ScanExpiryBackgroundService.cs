using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Pottmayer.Pandora.Modules.Files.Application.Commands.ExpireScans;
using Pottmayer.Tars.Core.Mediator.Abstractions;

namespace Pottmayer.Pandora.Modules.Files.Infrastructure.Jobs;

/// <summary>
/// Periodically aborts running scans whose agent went quiet — a crash, a sleeping laptop, a lost
/// connection — so nothing is ever applied from a half walk. Same shape as the other jobs: a
/// <see cref="PeriodicTimer"/> driving a CQRS command in a fresh scope.
/// </summary>
public sealed class ScanExpiryBackgroundService(
    IServiceProvider serviceProvider,
    TimeProvider timeProvider,
    ILogger<ScanExpiryBackgroundService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);

    /// <summary>A batch takes seconds; half an hour of silence means the agent is gone.</summary>
    private static readonly TimeSpan StaleAfter = TimeSpan.FromMinutes(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval, timeProvider);

        do
        {
            try
            {
                await using var scope = serviceProvider.CreateAsyncScope();
                var sender = scope.ServiceProvider.GetRequiredService<ISender>();
                var cutoff = timeProvider.GetUtcNow() - StaleAfter;

                var result = await sender.Send(new ExpireScansCommand(new ExpireScansInput(cutoff)), stoppingToken);
                if (result.IsSuccess && result.Value > 0)
                    logger.LogInformation("Aborted {Count} stale Files scan(s).", result.Value);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Files scan expiry failed.");
            }
        }
        while (await WaitAsync(timer, stoppingToken));
    }

    private static async Task<bool> WaitAsync(PeriodicTimer timer, CancellationToken ct)
    {
        try
        {
            return await timer.WaitForNextTickAsync(ct);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }
}
