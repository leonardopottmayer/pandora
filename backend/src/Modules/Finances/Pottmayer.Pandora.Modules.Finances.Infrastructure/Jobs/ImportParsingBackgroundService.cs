using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Pottmayer.Pandora.Modules.Finances.Application.Commands.RunImportParsing;
using Pottmayer.Tars.Core.Mediator.Abstractions;

namespace Pottmayer.Pandora.Modules.Finances.Infrastructure.Jobs;

public sealed class ImportParsingBackgroundService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);

    private readonly IServiceProvider _serviceProvider;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ImportParsingBackgroundService> _logger;

    public ImportParsingBackgroundService(
        IServiceProvider serviceProvider,
        TimeProvider timeProvider,
        ILogger<ImportParsingBackgroundService> logger)
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
                _logger.LogError(ex, "Import parsing job failed.");
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
        // Process one file per tick so we keep the job simple and avoid long-held transactions
        await using var scope = _serviceProvider.CreateAsyncScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        var result = await sender.Send(new RunImportParsingCommand(), ct);

        if (result.IsFailure)
        {
            _logger.LogWarning(
                "Import parsing command failed: {Errors}.",
                string.Join("; ", result.Errors.Select(e => $"{e.Code}: {e.Message}")));
        }
        else if (result.Value)
        {
            _logger.LogInformation("Import parsing job processed one file.");
        }
    }
}
