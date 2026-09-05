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
/// One poll loop per configured inbound bot (<see cref="TelegramChannelOptions.InboundBots"/>), each an
/// independent <c>getUpdates</c> consumer — separate tokens never collide on <c>409</c>. Still a single
/// consumer per bot, though: a second replica polling the same bot would. Each bot's offset — the id of
/// the next update to fetch, which also acks every earlier one — is restored from the recorded updates
/// (chn004) on startup, so a backlog is not replayed from the beginning after a restart.
/// </remarks>
public sealed class TelegramLongPollingService(
    IServiceProvider serviceProvider,
    ITelegramClientFactory telegram,
    IOptions<ChannelsOptions> options,
    ILogger<TelegramLongPollingService> logger) : BackgroundService
{
    private const string Provider = "telegram";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value.Telegram;
        if (!settings.LongPolling || settings.InboundBots.Length == 0)
            return;

        var pollTimeout = TimeSpan.FromSeconds(Math.Max(1, settings.PollTimeoutSeconds));

        // One loop per bot, each with its own offset. A crash in one bot's loop must not stop the others.
        var loops = settings.InboundBots
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(bot => PollBotAsync(bot, pollTimeout, stoppingToken));

        await Task.WhenAll(loops);
    }

    private async Task PollBotAsync(string bot, TimeSpan pollTimeout, CancellationToken stoppingToken)
    {
        var offset = await ResolveInitialOffsetAsync(bot, stoppingToken);

        logger.LogInformation("Telegram long polling started for bot {Bot} at offset {Offset}.", bot, offset);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = serviceProvider.CreateAsyncScope();
                // Resolved per cycle so each poll takes a fresh factory-managed HttpClient (handler rotation).
                var client = telegram.GetClient(bot);
                var triage = scope.ServiceProvider.GetRequiredService<TelegramInboundTriage>();

                var updates = await client.GetUpdatesAsync(offset, pollTimeout, stoppingToken);

                foreach (var update in updates)
                {
                    try
                    {
                        await triage.HandleAsync(bot, update, stoppingToken);
                    }
                    catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
                    {
                        // Advance past a poison update rather than loop on it; a restart replays it
                        // from the last recorded offset.
                        logger.LogError(ex, "Failed to process inbound update {UpdateId} on bot {Bot}.", update.UpdateId, bot);
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
                logger.LogError(ex, "Another consumer is polling bot {Bot} (409 Conflict). Backing off.", bot);
                await DelayAsync(TimeSpan.FromSeconds(30), stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Telegram long-poll cycle failed for bot {Bot}. Backing off.", bot);
                await DelayAsync(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }

    private async Task<long> ResolveInitialOffsetAsync(string bot, CancellationToken ct)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        var factory = scope.ServiceProvider.GetRequiredService<IUnitOfWorkFactory>();

        var lastId = await factory.ExecuteAsync(ChannelsModule.DatabaseKey, (context, token) =>
            context.AcquireRepository<IInboundUpdateRepository>().GetLastUpdateIdAsync(Provider, bot, token),
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
