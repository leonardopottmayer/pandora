using Pottmayer.Tars.Communication.Telegram.Abstractions;
using Pottmayer.Tars.Communication.Telegram.Abstractions.Models;

namespace Pottmayer.Pandora.Modules.Channels.Tests.Fakes;

/// <summary>
/// A hand-rolled <see cref="ITelegramClient"/> for transport tests. Only <see cref="SendMessageAsync"/>
/// is exercised; the rest throw so an accidental use is loud.
/// </summary>
public sealed class FakeTelegramClient : ITelegramClient
{
    public List<TelegramMessage> Sent { get; } = [];
    public List<string> AnsweredCallbacks { get; } = [];
    public long MessageId { get; set; } = 100;
    public Exception? Throw { get; set; }

    public Task<TelegramSendResult> SendMessageAsync(TelegramMessage message, CancellationToken cancellationToken = default)
    {
        if (Throw is not null)
            throw Throw;

        Sent.Add(message);
        return Task.FromResult(new TelegramSendResult(message.ChatId, MessageId, DateTimeOffset.UnixEpoch));
    }

    public Task AnswerCallbackQueryAsync(string callbackQueryId, string? text = null, CancellationToken cancellationToken = default)
    {
        AnsweredCallbacks.Add(callbackQueryId);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<TelegramUpdate>> GetUpdatesAsync(long offset, TimeSpan pollTimeout, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task<Stream> DownloadFileAsync(string fileId, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task SetWebhookAsync(Uri url, string secretToken, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task DeleteWebhookAsync(CancellationToken cancellationToken = default)
        => throw new NotSupportedException();
}
