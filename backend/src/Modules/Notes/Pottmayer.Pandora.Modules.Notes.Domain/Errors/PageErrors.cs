using Pottmayer.Tars.Core.Primitives.Outcomes;

namespace Pottmayer.Pandora.Modules.Notes.Domain.Errors;

public static class PageErrors
{
    public static Error NotFound =>
        Error.NotFound("Pages.NotFound", "Page not found.");

    public static Error InvalidTitle =>
        Error.Validation("Pages.InvalidTitle", "Page title is required.");

    /// <summary>The requested parent does not exist or is not owned by the user (404-on-foreign-resource rule).</summary>
    public static Error ParentNotFound =>
        Error.NotFound("Pages.ParentNotFound", "The parent page does not exist.");

    public static Error CycleDetected =>
        Error.Conflict("Pages.CycleDetected", "A page cannot be moved under itself or one of its descendants.");
}
