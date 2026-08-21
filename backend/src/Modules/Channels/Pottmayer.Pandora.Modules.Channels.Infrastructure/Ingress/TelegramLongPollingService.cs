using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Pottmayer.Pandora.Modules.Channels.Abstractions;
using Pottmayer.Pandora.Modules.Channels.Domain.Ports.Repositories;
using Pottmayer.Tars.Communication.Telegram.Abstractions;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Channels.Infrastructure.Ingress;

/// <summary>
/// Pulls inbound Telegram updates by long polling and hands each to the triage. Works behind NAT with
/// no public HTTPS, which is exactly the homelab, and is the ingress used until a webhook exists.
/// </summary>
/// <remarks>
/// Singleton by construction: <c>getUpdates</c> allows one consumer per bot token, and a second one
/// gets <c>409 Conflict</c>. The offset — the id of the next update to fetch, which also acks every
/// earlier one — is restored from the recorded updates (chn004) on startup, so a backlog is not
/// replayed from the beginning after a restart.
/// </remarks>
public sealed class TelegramLongPollingService(
    IServiceProvider serviceProvider,
    IOptions<ChannelsOptions> options,
    ILogger<TelegramLongPollingService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value.Telegram;
        if (!settings.LongPolling)
            return;

        var pollTimeout = TimeSpan.FromSeconds(Math.Max(1, settings.PollTimeoutSeconds));
        var offset = await ResolveInitialOffsetAsync(stoppingToken);

        logger.LogInformation("Telegram long polling started at offset {Offset}.", offset);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = serviceProvider.CreateAsyncScope();
                var client = scope.ServiceProvider.GetRequiredService<ITelegramClient>();
                var triage = scope.ServiceProvider.GetRequiredService<TelegramInboundTriage>();

                var updates = await client.GetUpdatesAsync(offset, pollTimeout, stoppingToken);

                foreach (var update in updates)
                {
                    try
                    {
                        await triage.HandleAsync(update, stoppingToken);
                    }
                    catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
                    {
                        // Advance past a poison update rather than loop on it; a restart replays it
                        // from the last recorded offset.
                        logger.LogError(ex, "Failed to process inbound update {UpdateId}.", update.UpdateId);
                    }

                    offset = Math.Max(offset, update.UpdateId + 1);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (TelegramException ex) when (ex.ErrorCode == 409)
            {
                logger.LogError(ex, "Another consumer is polling this bot (409 Conflict). Backing off.");
                await DelayAsync(TimeSpan.FromSeconds(30), stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Telegram long-poll cycle failed. Backing off.");
                await DelayAsync(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }

    private async Task<long> ResolveInitialOffsetAsync(CancellationToken ct)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        var factory = scope.ServiceProvider.GetRequiredService<IUnitOfWorkFactory>();

        var lastId = await factory.ExecuteAsync(ChannelsModule.Name, (context, token) =>
            context.AcquireRepository<IInboundUpdateRepository>().GetLastUpdateIdAsync("telegram", token),
            cancellationToken: ct);

        return lastId is { } id ? id + 1 : 0;
    }

    private static async Task DelayAsync(TimeSpan delay, CancellationToken ct)
    {
        try
        {
            await Task.Delay(delay, ct);
        }
        catch (OperationCanceledException)
        {
            // Shutting down.
        }
    }
}
