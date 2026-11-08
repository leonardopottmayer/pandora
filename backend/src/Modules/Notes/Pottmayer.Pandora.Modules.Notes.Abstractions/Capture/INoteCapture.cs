using Pottmayer.Tars.Core.Primitives.Outcomes;

namespace Pottmayer.Pandora.Modules.Notes.Abstractions.Capture;

/// <summary>Captures an explicit inbound note without sending its text to an AI provider.</summary>
public interface INoteCapture
{
    Task<Result<NoteCaptureResult>> CaptureAsync(NoteCaptureRequest request, CancellationToken ct = default);
}
