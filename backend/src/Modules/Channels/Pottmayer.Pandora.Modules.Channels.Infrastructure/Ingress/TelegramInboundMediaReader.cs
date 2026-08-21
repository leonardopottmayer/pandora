using Pottmayer.Pandora.Modules.Channels.Abstractions;
using Pottmayer.Pandora.Modules.Channels.Domain.ValueObjects;
using Pottmayer.Tars.Communication.Telegram.Abstractions;

namespace Pottmayer.Pandora.Modules.Channels.Infrastructure.Ingress;

/// <summary>
/// Opens inbound media by downloading it from Telegram. The media ref is the Bot API <c>file_id</c>.
/// The only channel it serves is Telegram; another channel would be another implementation.
/// </summary>
public sealed class TelegramInboundMediaReader(ITelegramClient client) : IInboundMediaReader
{
    public async Task<Stream> OpenAsync(string channel, string mediaRef, CancellationToken ct = default)
    {
        if (!string.Equals(channel, Channel.Telegram.Value, StringComparison.OrdinalIgnoreCase))
            throw new NotSupportedException($"No inbound media reader for channel '{channel}'.");

        return await client.DownloadFileAsync(mediaRef, ct);
    }
}
