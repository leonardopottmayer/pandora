using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Pottmayer.Pandora.Modules.Channels.Abstractions;
using Pottmayer.Pandora.Modules.Channels.Application.Commands.PurgeInboundUpdates;
using Pottmayer.Tars.Core.Mediator.Abstractions;

namespace Pottmayer.Pandora.Modules.Channels.Infrastructure.Jobs;

/// <summary>
/// Periodically clears aged-out raw inbound payloads (<c>chn004.raw</c>), keeping the rows themselves.
/// Mirrors the <c>RefreshTokenPurgeBackgroundService</c> pattern: a <see cref="PeriodicTimer"/> driving
/// a CQRS command in a fresh scope. Does nothing when <see cref="RawRetentionOptions.Enabled"/> is off.
/// </summary>
public sealed class InboundUpdateRetentionBackgroundService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(24);

    private readonly IServiceProvider _serviceProvider;
    private readonly TimeProvider _timeProvider;
    private readonly RawRetentionOptions _options;
    private readonly ILogger<InboundUpdateRetentionBackgroundService> _logger;

    public InboundUpdateRetentionBackgroundService(
        IServiceProvider serviceProvider,
        TimeProvider timeProvider,
        IOptions<ChannelsOptions> options,
        ILogger<InboundUpdateRetentionBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _timeProvider = timeProvider;
        _options = options.Value.RawRetention;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("Inbound raw retention purge is disabled; job not running.");
            return;
        }

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
                _logger.LogError(ex, "Inbound raw retention purge failed.");
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

        var retentionDays = Math.Max(1, _options.RetentionDays);
        var cutoff = _timeProvider.GetUtcNow() - TimeSpan.FromDays(retentionDays);
        var command = new PurgeInboundUpdatesCommand(new PurgeInboundUpdatesInput(cutoff));

        var result = await sender.Send(command, ct).ConfigureAwait(false);

        if (result.IsSuccess && result.Value > 0)
            _logger.LogInformation("Cleared raw payload of {Count} inbound update(s).", result.Value);
        else if (result.IsFailure)
            _logger.LogWarning(
                "Inbound raw retention purge command failed: {Errors}.",
                string.Join("; ", result.Errors.Select(e => $"{e.Code}: {e.Message}")));
    }
}
