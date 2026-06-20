using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Pottmayer.Pandora.Modules.Finances.Application.Commands.RunStatementLifecycle;
using Pottmayer.Tars.Core.Mediator.Abstractions;

namespace Pottmayer.Pandora.Modules.Finances.Infrastructure.Jobs;

public sealed class StatementLifecycleBackgroundService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(24);

    private readonly IServiceProvider _serviceProvider;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<StatementLifecycleBackgroundService> _logger;

    public StatementLifecycleBackgroundService(
        IServiceProvider serviceProvider,
        TimeProvider timeProvider,
        ILogger<StatementLifecycleBackgroundService> logger)
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
                _logger.LogError(ex, "Statement lifecycle job failed.");
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
        try
        {
            await RunOnceAsync(ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (JobConcurrency.IsUniqueViolation(ex))
        {
            // The recurrence job created the same statement concurrently; re-run so the existence
            // checks see the now-committed row and skip it.
            _logger.LogInformation("Statement lifecycle hit a concurrent statement creation; retrying once.");
            await RunOnceAsync(ct).ConfigureAwait(false);
        }
    }

    private async Task RunOnceAsync(CancellationToken ct)
    {
        await using var scope = _serviceProvider.CreateAsyncScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var today = DateOnly.FromDateTime(_timeProvider.GetUtcNow().UtcDateTime);
        var result = await sender.Send(new RunStatementLifecycleCommand(new RunStatementLifecycleInput(today)), ct);

        if (result.IsFailure)
        {
            _logger.LogWarning(
                "Statement lifecycle command failed: {Errors}.",
                string.Join("; ", result.Errors.Select(e => $"{e.Code}: {e.Message}")));
            return;
        }

        if (result.Value > 0)
            _logger.LogInformation("Statement lifecycle processed {Count} statement mutation(s).", result.Value);
    }
}
