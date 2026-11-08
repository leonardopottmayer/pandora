using Pottmayer.Tars.Core.Primitives.Outcomes;

namespace Pottmayer.Pandora.Modules.Notes.Domain.Errors;

public static class PageErrors
{
    public static Error HistoryConflict =>
        Error.Conflict("Pages.HistoryConflict", "The page changed. Reload it before saving or restoring.");

    public static Error InvalidRestore =>
        Error.Conflict("Pages.InvalidRestore", "This version cannot be restored to its original location.");

    public static Error NotFound =>
        Error.NotFound("Pages.NotFound", "Page not found.");

    /// <summary>The blocks sent are not a valid page document; the reason says which rule failed.</summary>
    public static Error InvalidContent(string reason) =>
        Error.Validation("Pages.InvalidContent", reason);

    public static Error InvalidTitle =>
        Error.Validation("Pages.InvalidTitle", "Page title is required.");

    public static Error TitleTooLong =>
        Error.Validation("Pages.TitleTooLong", "Page title must be at most 255 characters.");

    /// <summary>The requested parent does not exist or is not owned by the user (404-on-foreign-resource rule).</summary>
    public static Error ParentNotFound =>
        Error.NotFound("Pages.ParentNotFound", "The parent page does not exist.");

    public static Error CycleDetected =>
        Error.Conflict("Pages.CycleDetected", "A page cannot be moved under itself or one of its descendants.");

    public static Error InvalidAppearance(string reason) =>
        Error.Validation("Pages.InvalidAppearance", reason);

    /// <summary>The page is not in the trash: it is live, someone else's, or gone for good.</summary>
    public static Error NotInTrash =>
        Error.NotFound("Pages.NotInTrash", "The page is not in the trash.");

    public static Error InvalidVerification =>
        Error.Validation("Pages.InvalidVerification", "A verification lasts from 1 to 365 days, or does not run out.");
}
