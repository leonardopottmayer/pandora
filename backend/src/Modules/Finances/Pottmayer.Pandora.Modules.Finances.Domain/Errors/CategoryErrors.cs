using Pottmayer.Tars.Core.Primitives.Outcomes;

namespace Pottmayer.Pandora.Modules.Finances.Domain.Errors;

public static class CategoryErrors
{
    public static Error NotFound =>
        Error.NotFound("Categories.NotFound", "Category not found.");

    public static Error ParentNotFound =>
        Error.Validation("Categories.ParentNotFound", "The parent category does not exist.");

    public static Error InvalidName =>
        Error.Validation("Categories.InvalidName", "Category name is required.");

    public static Error InvalidNature(string nature) =>
        Error.Validation("Categories.InvalidNature", $"Transaction nature '{nature}' is not supported.");

    public static Error NameAlreadyExists =>
        Error.Conflict("Categories.NameAlreadyExists", "A category with this name already exists under the same parent.");

    public static Error ParentIsNotRoot =>
        Error.Validation("Categories.ParentIsNotRoot", "Categories support at most two levels; the chosen parent is already a child.");

    public static Error NatureMismatch =>
        Error.Validation("Categories.NatureMismatch", "A child category must have the same nature as its parent.");
}
