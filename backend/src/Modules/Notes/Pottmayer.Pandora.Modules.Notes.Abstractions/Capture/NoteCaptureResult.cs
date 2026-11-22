namespace Pottmayer.Pandora.Modules.Notes.Abstractions.Capture;

/// <summary>The destination page, or the receipt of an already captured event.</summary>
public sealed record NoteCaptureResult(Guid PageId, string Title, bool IsDuplicate);
