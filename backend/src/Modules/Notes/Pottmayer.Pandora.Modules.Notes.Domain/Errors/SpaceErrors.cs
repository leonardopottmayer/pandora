using Pottmayer.Tars.Core.Primitives.Outcomes;

namespace Pottmayer.Pandora.Modules.Notes.Domain.Errors;

public static class SpaceErrors
{
    public static Error NotFound =>
        Error.NotFound("Spaces.NotFound", "Space not found.");

    public static Error InvalidName =>
        Error.Validation("Spaces.InvalidName", "Space name is required.");

    public static Error InvalidColor =>
        Error.Validation("Spaces.InvalidColor", "Space color must be a hex color.");

    /// <summary>Pages would be left pointing at a deleted space: archive it, or move them first.</summary>
    public static Error NotEmpty =>
        Error.Conflict("Spaces.NotEmpty", "The space still has pages. Move them or archive the space instead.");

    /// <summary>Pages with no space named go to the default one, so it has to stay reachable.</summary>
    public static Error IsDefault =>
        Error.Conflict("Spaces.IsDefault", "The default space cannot be archived or deleted. Make another space the default first.");
}
