using Pottmayer.Tars.Core.Primitives.Outcomes;

namespace Pottmayer.Pandora.Modules.Notes.Domain.Errors;

public static class CaptureErrors
{
    public static Error InvalidText => Error.Validation("Notes.InvalidCapture", "Send /nota followed by up to 8000 characters of text.");
    public static Error Locked => Error.Conflict("Notes.CaptureLocked", "The capture page is locked. Unlock it before capturing another note.");
}
