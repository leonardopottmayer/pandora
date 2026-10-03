using Microsoft.Extensions.Logging;
using Pottmayer.Pandora.Modules.Assistant.Abstractions;
using Pottmayer.Pandora.Modules.Assistant.Abstractions.Commands;
using Pottmayer.Pandora.Modules.Assistant.Abstractions.Files;
using Pottmayer.Pandora.Modules.Assistant.Application.Commands.Interpret;
using Pottmayer.Pandora.Modules.Assistant.Application.Dtos;
using Pottmayer.Pandora.Modules.Assistant.Application.Interpret;
using Pottmayer.Pandora.Modules.Assistant.Domain.ValueObjects;
using Pottmayer.Pandora.Modules.Channels.Abstractions;
using Pottmayer.Pandora.Modules.Channels.Contracts;
using Pottmayer.Tars.Ai.Chat.Abstractions.Models;
using Pottmayer.Tars.Core.Mediator.Abstractions;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;
using Pottmayer.Tars.Messaging.Abstractions;

namespace Pottmayer.Pandora.Modules.Assistant.Application.Subscribers;

/// <summary>
/// The Telegram side of the assistant: an inbound message that arrived on the assistant bot is interpreted
/// exactly like the web command bar, and whatever it produced is sent back to the user through Channels.
/// Runs one message at a time as an integration-event subscriber — the latency and durability the async
/// path was meant to give — and speaks only in domain terms, never touching the Bot API.
/// </summary>
/// <remarks>
/// Only the assistant bot's inbound is a conversation; messages on other bots (e.g. notifications) are not
/// this handler's to interpret. The conversation is resolved channel-agnostically inside the interpret
/// pipeline (no conversation id → the user's most recent non-expired thread), so a follow-up typed on
/// Telegram continues one begun on the web, and vice versa. A voice note is downloaded through Channels'
/// <see cref="IInboundMediaReader"/> and handed to the pipeline to transcribe; the reply echoes what was
/// heard. A call held for confirmation gets Confirm / Cancel buttons, answered by
/// <see cref="AssistantInteractionReceivedHandler"/>. Any other file (an image or a PDF — a receipt, a
/// boleto) is not interpreted: its caption names the module queue it goes to (<see cref="IAssistantFileQueue"/>),
/// and the user files it there from the app.
/// </remarks>
public sealed class InboundMessageReceivedHandler(
    IUnitOfWorkFactory factory,
    ISender sender,
    IIntegrationEventBus bus,
    IInboundMediaReader media,
    IEnumerable<IAssistantFileQueue> fileQueues,
    AssistantToolContextResolver contexts,
    TimeProvider timeProvider,
    ILogger<InboundMessageReceivedHandler> logger)
    : IIntegrationEventHandler<InboundMessageReceived>
{
    public async Task HandleAsync(InboundMessageReceived @event, CancellationToken cancellationToken = default)
    {
        if (!string.Equals(@event.Bot, AssistantModule.Name, StringComparison.OrdinalIgnoreCase))
            return;

        var isAudio = @event.MediaMimeType?.StartsWith("audio/", StringComparison.OrdinalIgnoreCase) == true;
        if (@event.MediaRef is not null && !isAudio)
        {
            var context = await contexts.ResolveAsync(@event.UserId, cancellationToken);
            await ReplyAsync(@event.UserId, await QueueFileAsync(@event, context, cancellationToken), null, cancellationToken);
            return;
        }

        ChatAttachment? audio = null;
        if (string.IsNullOrWhiteSpace(@event.Text))
        {
            if (@event.MediaRef is null)
                return;

            audio = new ChatAttachment(
                await ReadMediaAsync(@event, InterpretCommandHandler.MaxAudioBytes, cancellationToken), @event.MediaMimeType!);
        }

        var result = await sender.Send(
            new InterpretCommand(new InterpretInput(@event.UserId, @event.Text, Audio: audio)), cancellationToken);
        if (!result.IsSuccess)
        {
            // Pre-flight failures only reach here (assistant disabled, no API key): nothing to say to the
            // user that would not leak configuration. The web settings screen is where they fix it.
            logger.LogWarning("Interpret rejected an inbound assistant message from user {UserId}.", @event.UserId);
            return;
        }

        var reply = result.Value.Message;
        if (result.Value.Transcript is { } heard)
            reply = $"🎤 “{heard}”\n\n{reply}";

        if (string.IsNullOrWhiteSpace(reply))
            return;

        await ReplyAsync(@event.UserId, reply, await ButtonsAsync(@event.UserId, result.Value.Invocations, cancellationToken), cancellationToken);
    }

    /// <summary>
    /// Confirm / Cancel for every held call, carrying its invocation id. Numbered when there are several,
    /// matching the numbered lines of the reply.
    /// </summary>
    private async Task<IReadOnlyList<NotificationButton>?> ButtonsAsync(
        Guid userId, IReadOnlyList<InvocationResultDto> invocations, CancellationToken ct)
    {
        var pending = invocations
            .Select((invocation, i) => (invocation, number: i + 1))
            .Where(x => x.invocation.Status == InvocationStatus.PendingConfirmation.Value)
            .ToList();
        if (pending.Count == 0)
            return null;

        var context = await contexts.ResolveAsync(userId, ct);
        var confirm = context.Text("✅ Confirmar", "✅ Confirm");
        var cancel = context.Text("❌ Cancelar", "❌ Cancel");
        string Suffix(int number) => invocations.Count > 1 ? $" {number}" : string.Empty;

        return pending
            .SelectMany(x => new[]
            {
                new NotificationButton(AssistantModule.Name, AssistantInteractionReceivedHandler.ConfirmAction,
                    confirm + Suffix(x.number), x.invocation.InvocationId.ToString()),
                new NotificationButton(AssistantModule.Name, AssistantInteractionReceivedHandler.CancelAction,
                    cancel + Suffix(x.number), x.invocation.InvocationId.ToString()),
            })
            .ToList();
    }

    /// <summary>
    /// Parks a shared file in the queue its caption names and says where it went — or why it did not:
    /// not an image or PDF, no queue named, or larger than a bot may download.
    /// </summary>
    private async Task<string> QueueFileAsync(InboundMessageReceived @event, AssistantToolContext context, CancellationToken ct)
    {
        var contentType = @event.MediaMimeType ?? "application/octet-stream";
        if (!SharedFile.IsSupported(contentType))
            return context.Text("📎 Só guardo imagens e PDFs.", "📎 I only keep images and PDFs.");

        var caption = @event.Text?.Trim() ?? string.Empty;
        var words = SharedFile.WordsOf(caption);
        var queue = fileQueues.FirstOrDefault(q => q.Keywords.Any(words.Contains));
        if (queue is null)
        {
            var examples = string.Join(", ", fileQueues.SelectMany(q => q.Keywords.Take(3)).Select(k => $"“{k}”"));
            return context.Text(
                $"📎 Para onde vai esse arquivo? Mande de novo com uma legenda dizendo — por exemplo: {examples}.",
                $"📎 Where does this file go? Send it again with a caption saying so — e.g. {examples}.");
        }

        var tooLarge = context.Text(
            "📎 Arquivo grande demais: o Telegram só entrega arquivos de até 20 MB a bots.",
            "📎 That file is too large: Telegram only hands bots files up to 20 MB.");
        if (@event.MediaSizeBytes > SharedFile.MaxBytes)
            return tooLarge;

        var content = await ReadMediaAsync(@event, SharedFile.MaxBytes, ct);
        if (content.Length > SharedFile.MaxBytes)
            return tooLarge;

        var file = new SharedFile(@event.UserId, @event.MediaFileName ?? DefaultFileName(contentType), contentType, content, caption);
        return await queue.EnqueueAsync(file, ct)
            ? context.Text($"📎 Na fila do {queue.Name}: {file.FileName}. Atribua pelo app.",
                $"📎 Queued in {queue.Name}: {file.FileName}. File it from the app.")
            : context.Text("📎 Não consegui guardar o arquivo.", "📎 I couldn't keep that file.");
    }

    /// <summary>A photo comes without a name: <c>telegram-20261003-143000.jpg</c>.</summary>
    private string DefaultFileName(string contentType)
    {
        var extension = contentType[(contentType.IndexOf('/') + 1)..] switch { "jpeg" => "jpg", var other => other };
        return $"telegram-{timeProvider.GetUtcNow():yyyyMMdd-HHmmss}.{extension}";
    }

    /// <summary>
    /// Reads the media into memory, stopping one byte past <paramref name="limit"/> so an oversized file is
    /// refused (with a reply in the user's language) without buffering all of it.
    /// </summary>
    private async Task<byte[]> ReadMediaAsync(InboundMessageReceived @event, long limit, CancellationToken ct)
    {
        await using var stream = await media.OpenAsync(@event.Channel, @event.Bot, @event.MediaRef!, ct);
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await stream.ReadAsync(chunk, ct)) > 0)
        {
            buffer.Write(chunk, 0, read);
            if (buffer.Length > limit)
                break;
        }

        return buffer.ToArray();
    }

    /// <summary>
    /// Asks Channels to deliver a reply on the assistant bot. Published in a unit of work so it rides the
    /// transactional outbox rather than being lost on a crash after the interpret committed.
    /// </summary>
    private Task ReplyAsync(Guid userId, string text, IReadOnlyList<NotificationButton>? buttons, CancellationToken ct) =>
        factory.ExecuteAsync(AssistantModule.DatabaseKey, async (context, token) =>
        {
            await bus.PublishAsync(
                new SendAssistantReply(Guid.CreateVersion7(), timeProvider.GetUtcNow(), userId, AssistantModule.Name, text, buttons),
                token);
            return true;
        }, cancellationToken: ct);
}
