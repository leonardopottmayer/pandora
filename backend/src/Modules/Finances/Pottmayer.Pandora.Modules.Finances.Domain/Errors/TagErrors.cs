using Pottmayer.Tars.Core.Primitives.Outcomes;

namespace Pottmayer.Pandora.Modules.Finances.Domain.Errors;

public static class TagErrors
{
    public static Error NotFound =>
        Error.NotFound("Tags.NotFound", "Tag not found.");

    public static Error InvalidName =>
        Error.Validation("Tags.InvalidName", "Tag name is required.");

    public static Error NameAlreadyExists =>
        Error.Conflict("Tags.NameAlreadyExists", "A tag with this name already exists.");

    public static Error InvalidEntityType(string entityType) =>
        Error.Validation("Tags.InvalidEntityType", $"Entity type '{entityType}' is not supported.");

    /// <summary>The link target does not exist or is not owned by the user (404-on-foreign-resource rule).</summary>
    public static Error TargetNotFound =>
        Error.NotFound("Tags.TargetNotFound", "The entity to tag does not exist.");

    public static Error LinkNotFound =>
        Error.NotFound("Tags.LinkNotFound", "The tag is not linked to this entity.");
}
