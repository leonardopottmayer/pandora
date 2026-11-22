using Pottmayer.Tars.Core.Primitives.Outcomes;

namespace Pottmayer.Pandora.Modules.Notes.Domain.Errors;

public static class TagErrors
{
    public static Error NotFound =>
        Error.NotFound("Tags.NotFound", "Tag not found.");

    /// <summary>Tags are born from the text: there is no name to set here, only a color.</summary>
    public static Error InvalidColor =>
        Error.Validation("Tags.InvalidColor", "Tag color must be a valid CSS color.");
}
