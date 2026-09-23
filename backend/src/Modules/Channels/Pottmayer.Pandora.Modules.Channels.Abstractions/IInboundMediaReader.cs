namespace Pottmayer.Pandora.Modules.Channels.Abstractions;

/// <summary>
/// Opens the bytes of a piece of media the user sent inbound, by the opaque reference carried on
/// <c>InboundMessageReceived</c>. This is the one thing the Assistant calls on Channels — it is what
/// lets that module transcribe a voice note without ever learning that Telegram exists.
/// </summary>
public interface IInboundMediaReader
{
    /// <summary>
    /// Opens the media stream. <paramref name="bot"/> is the inbound route the message arrived on (the
    /// event's <c>Bot</c>) — a Telegram <c>file_id</c> is only valid for the bot that received it. The
    /// caller owns and disposes the stream.
    /// </summary>
    Task<Stream> OpenAsync(string channel, string bot, string mediaRef, CancellationToken ct = default);
}
