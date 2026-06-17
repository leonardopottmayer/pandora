using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Pottmayer.Pandora.Modules.Finances.Application.Commands.RunRecurrenceGeneration;
using Pottmayer.Tars.Core.Mediator.Abstractions;

namespace Pottmayer.Pandora.Modules.Finances.Infrastructure.Jobs;

public sealed class RecurrenceGenerationBackgroundService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(24);

    private readonly IServiceProvider _serviceProvider;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<RecurrenceGenerationBackgroundService> _logger;

    public RecurrenceGenerationBackgroundService(
        IServiceProvider serviceProvider,
        TimeProvider timeProvider,
        ILogger<RecurrenceGenerationBackgroundService> logger)
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
                await RunAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Recurrence generation job failed.");
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

    private async Task RunAsync(CancellationToken ct)
    {
        await using var scope = _serviceProvider.CreateAsyncScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var today = DateOnly.FromDateTime(_timeProvider.GetUtcNow().UtcDateTime);
        var result = await sender.Send(new RunRecurrenceGenerationCommand(new RunRecurrenceGenerationInput(today)), ct);

        if (result.IsFailure)
        {
            _logger.LogWarning(
                "Recurrence generation command failed: {Errors}.",
                string.Join("; ", result.Errors.Select(e => $"{e.Code}: {e.Message}")));
            return;
        }

        if (result.Value > 0)
            _logger.LogInformation("Recurrence generation produced {Count} occurrence(s).", result.Value);
    }
}
