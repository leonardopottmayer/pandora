using Pottmayer.Pandora.Modules.Notes.Abstractions.Capture;
using Pottmayer.Pandora.Modules.Notes.Application.Commands.CaptureNote;
using Pottmayer.Tars.Core.Mediator.Abstractions;
using Pottmayer.Tars.Core.Primitives.Outcomes;

namespace Pottmayer.Pandora.Modules.Notes.Application.Services;

/// <summary>Routes capture requests through the authorized Notes command.</summary>
public sealed class NoteCaptureService(ISender sender) : INoteCapture
{
    public async Task<Result<NoteCaptureResult>> CaptureAsync(NoteCaptureRequest request, CancellationToken ct = default)
        => await sender.Send(new CaptureNoteCommand(request), ct);
}
