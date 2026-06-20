using Pottmayer.Tars.Core.Primitives.Outcomes;

namespace Pottmayer.Pandora.Modules.Finances.Domain.Errors;

public static class ImportErrors
{
    public static readonly Error NotFound =
        Error.NotFound("Imports.NotFound", "Import file not found.");

    public static readonly Error LayoutNotDetected =
        Error.Validation("Imports.LayoutNotDetected",
            "The file format could not be identified. Supported formats: OFX and CSV exports from Nubank, Itaú, Banco Inter, and Viacredi.");

    public static readonly Error InvalidDestination =
        Error.Validation("Imports.InvalidDestination",
            "The file contains card data but the destination is an account, or vice-versa.");

    public static readonly Error AlreadyTerminal =
        Error.Conflict("Imports.AlreadyTerminal", "The import has already been completed or aborted.");

    public static readonly Error NotFailed =
        Error.Conflict("Imports.NotFailed", "Only failed imports can be retried.");

    public static readonly Error FileTooLarge =
        Error.Validation("Imports.FileTooLarge", "The file exceeds the 10 MB limit.");
}
