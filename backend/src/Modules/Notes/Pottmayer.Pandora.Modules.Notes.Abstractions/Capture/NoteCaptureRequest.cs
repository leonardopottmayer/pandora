namespace Pottmayer.Pandora.Modules.Notes.Abstractions.Capture;

/// <summary>The stable inbound event identity and the text the user explicitly asked to capture.</summary>
public sealed record NoteCaptureRequest(Guid UserId, Guid MessageId, string Text, DateTimeOffset ReceivedAt, string Locale);
