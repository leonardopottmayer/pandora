using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Pottmayer.Pandora.Modules.Channels.Abstractions;
using Pottmayer.Pandora.Modules.Channels.Application.Commands.ConsumeTelegramLink;
using Pottmayer.Pandora.Modules.Channels.Contracts;
using Pottmayer.Pandora.Modules.Channels.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Channels.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Channels.Domain.ValueObjects;
using Pottmayer.Tars.Communication.Telegram.Abstractions;
using Pottmayer.Tars.Communication.Telegram.Abstractions.Models;
using Pottmayer.Tars.Core.Mediator.Abstractions;
using Pottmayer.Tars.Data.Abstractions.DataContext;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;
using Pottmayer.Tars.Messaging.Abstractions;

namespace Pottmayer.Pandora.Modules.Channels.Infrastructure.Ingress;

/// <summary>
/// Classifies one inbound Telegram update by its structure and acts: links a chat on
/// <c>/start &lt;token&gt;</c>, answers other slash commands locally, and turns free text or media
/// from a linked user into an <see cref="InboundMessageReceived"/> event. It never interprets meaning
/// — that is the Assistant's job.
/// </summary>
/// <remarks>
/// Inline-button callbacks (<see cref="InboundClassification.Interaction"/>) are acknowledged and
/// dropped for now: routing them needs the interaction table (chn003), which only earns its place
/// once a module actually produces buttons.
/// </remarks>
public sealed class TelegramInboundTriage(
    IUnitOfWorkFactory factory,
    IIntegrationEventBus bus,
    ISender sender,
    ITelegramClient client,
    TimeProvider timeProvider,
    ILogger<TelegramInboundTriage> logger)
{
    private const string Provider = "telegram";

    private sealed record Outcome(InboundClassification Classification, Guid? UserId, IIntegrationEvent? Event);

    private sealed record CallbackResolution(Guid? UserId, Interaction? Consumed);

    public async Task HandleAsync(TelegramUpdate update, CancellationToken ct)
    {
        // Idempotency: a replayed poll must not act on the same update twice.
        var alreadySeen = await factory.ExecuteAsync(ChannelsModule.DatabaseKey, (context, token) =>
            context.AcquireRepository<IInboundUpdateRepository>().ExistsAsync(Provider, update.UpdateId, token),
            cancellationToken: ct);
        if (alreadySeen)
            return;

        var outcome = await RouteAsync(update, ct);

        // Record the update durably — this is what restores the long-polling offset after a restart.
        await factory.ExecuteAsync(ChannelsModule.DatabaseKey, async (context, token) =>
        {
            var updates = context.AcquireRepository<IInboundUpdateRepository>();
            var record = InboundUpdate.Record(
                Provider, update.UpdateId, JsonSerializer.Serialize(update),
                outcome.UserId, outcome.Classification, timeProvider);
            record.MarkProcessed(timeProvider);
            await updates.AddAsync(record, token);

            // Published inside the unit of work: the event and the durable record of the update it
            // came from commit together (transactional outbox).
            if (outcome.Event is { } evt)
                await bus.PublishAsync(evt, token);
        }, cancellationToken: ct);
    }

    private async Task<Outcome> RouteAsync(TelegramUpdate update, CancellationToken ct)
    {
        if (update.CallbackQuery is { } callback)
            return await HandleCallbackAsync(callback, ct);

        if (update.Message is not { } message)
            return new Outcome(InboundClassification.Discarded, UserId: null, Event: null);

        var chatId = ChatId(message.Chat);

        if (TelegramCommand.TryParse(message.Text, out var command, out var argument))
            return await HandleCommandAsync(chatId, message, command, argument, ct);

        var userId = await ResolveUserAsync(chatId, ct);
        if (userId is null)
        {
            await ReplyAsync(chatId, "I don't know this chat yet. Connect Telegram from Pandora settings to get started.", ct);
            return new Outcome(InboundClassification.Discarded, UserId: null, Event: null);
        }

        var media = message.Media;
        var evt = new InboundMessageReceived(
            Guid.CreateVersion7(), timeProvider.GetUtcNow(), userId.Value, Provider,
            message.Text, media?.FileId, media?.MimeType);
        return new Outcome(InboundClassification.Message, userId, evt);
    }

    private async Task<Outcome> HandleCommandAsync(
        string chatId, TelegramIncomingMessage message, string command, string? argument, CancellationToken ct)
    {
        if (command == "start" && !string.IsNullOrWhiteSpace(argument))
        {
            var result = await sender.Send(
                new ConsumeTelegramLinkCommand(new ConsumeTelegramLinkInput(
                    chatId, argument!, message.From?.Username, message.From?.FirstName)),
                ct);

            await ReplyAsync(chatId, result.IsSuccess
                ? "You're connected. Pandora will reach you here."
                : "That link is invalid or has expired. Start again from Pandora settings.", ct);

            return new Outcome(InboundClassification.Command, result.IsSuccess ? result.Value : null, Event: null);
        }

        // Every other command is answered locally and never becomes an event.
        await ReplyAsync(chatId,
            "I forward your Pandora notifications and take your notes. Connect from Pandora settings if you haven't.", ct);
        return new Outcome(InboundClassification.Command, await ResolveUserAsync(chatId, ct), Event: null);
    }

    private async Task<Outcome> HandleCallbackAsync(TelegramCallbackQuery callback, CancellationToken ct)
    {
        var chatId = callback.Chat is { } chat ? ChatId(chat) : null;

        // Resolve the sender, then the button it points at, and burn it — all in one unit of work, so
        // a double tap cannot act twice.
        var resolution = await factory.ExecuteAsync(ChannelsModule.DatabaseKey, async (context, token) =>
        {
            Guid? userId = chatId is null ? null : await ResolveUserInContextAsync(context, chatId, token);

            if (userId is null || !Guid.TryParse(callback.Data, out var interactionId))
                return new CallbackResolution(userId, Consumed: null);

            var interactions = context.AcquireRepository<IInteractionRepository>();
            var interaction = await interactions.GetByIdAsync(interactionId, token);

            // Authenticity is the user on the row, never the client: a tap only counts for its owner.
            if (interaction is null || interaction.UserId != userId || !interaction.IsUsable(timeProvider.GetUtcNow()))
                return new CallbackResolution(userId, Consumed: null);

            interaction.Consume(timeProvider);
            await interactions.UpdateAsync(interaction, token);
            return new CallbackResolution(userId, interaction);
        }, cancellationToken: ct);

        if (resolution.Consumed is { } acted)
        {
            await TryAsync(client.AnswerCallbackQueryAsync(callback.Id, "Done.", ct));
            var evt = new InboundInteractionReceived(
                Guid.CreateVersion7(), timeProvider.GetUtcNow(), acted.UserId, Provider,
                acted.OwnerModule, acted.Action, acted.Payload);
            return new Outcome(InboundClassification.Interaction, resolution.UserId, evt);
        }

        await TryAsync(client.AnswerCallbackQueryAsync(callback.Id, "This button is no longer active.", ct));
        return new Outcome(InboundClassification.Interaction, resolution.UserId, Event: null);
    }

    private Task<Guid?> ResolveUserAsync(string chatId, CancellationToken ct) =>
        factory.ExecuteAsync(ChannelsModule.DatabaseKey,
            (context, token) => ResolveUserInContextAsync(context, chatId, token), cancellationToken: ct);

    private static async Task<Guid?> ResolveUserInContextAsync(IDataContext context, string chatId, CancellationToken ct)
    {
        var address = NotificationAddress.Create(Channel.Telegram, chatId);
        var link = await context.AcquireRepository<IUserChannelRepository>()
            .FindByAddressAsync(Channel.Telegram, address, ct);
        return link?.UserId;
    }

    private async Task ReplyAsync(string chatId, string text, CancellationToken ct) =>
        await TryAsync(client.SendMessageAsync(new TelegramMessage(chatId, text), ct));

    // Best-effort outbound: a failed reply must not poison the update, which still gets recorded.
    private async Task TryAsync(Task work)
    {
        try
        {
            await work;
        }
        catch (TelegramException ex)
        {
            logger.LogWarning(ex, "Telegram reply failed during inbound triage.");
        }
    }

    private static string ChatId(TelegramChat chat) => chat.Id.ToString(CultureInfo.InvariantCulture);
}
