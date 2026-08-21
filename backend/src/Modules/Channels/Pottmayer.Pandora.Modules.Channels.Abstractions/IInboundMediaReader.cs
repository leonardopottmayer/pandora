namespace Pottmayer.Pandora.Modules.Channels.Abstractions;

/// <summary>
/// Opens the bytes of a piece of media the user sent inbound, by the opaque reference carried on
/// <c>InboundMessageReceived</c>. This is the one thing the Assistant calls on Channels — it is what
/// lets that module transcribe a voice note without ever learning that Telegram exists.
/// </summary>
public interface IInboundMediaReader
{
    /// <summary>Opens the media stream. The caller owns and disposes it.</summary>
    Task<Stream> OpenAsync(string channel, string mediaRef, CancellationToken ct = default);
}
