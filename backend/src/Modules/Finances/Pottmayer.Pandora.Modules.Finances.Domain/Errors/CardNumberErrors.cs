using Pottmayer.Tars.Core.Primitives.Outcomes;

namespace Pottmayer.Pandora.Modules.Finances.Domain.Errors;

public static class CardNumberErrors
{
    public static Error NotFound =>
        Error.NotFound("CardNumbers.NotFound", "Card number not found.");

    public static Error InvalidLastFour =>
        Error.Validation("CardNumbers.InvalidLastFour", "Card last four must contain exactly 4 digits.");

    public static Error InvalidKind(string kind) =>
        Error.Validation("CardNumbers.InvalidKind", $"Card number kind '{kind}' is invalid.");

    public static Error LastFourAlreadyExists =>
        Error.Conflict("CardNumbers.LastFourAlreadyExists", "This card already has a number ending in these digits.");

    public static Error Archived =>
        Error.Conflict("CardNumbers.Archived", "An archived card number cannot be changed or used.");

    public static Error NotOnCard =>
        Error.Validation("CardNumbers.NotOnCard", "The card number does not belong to the transaction's card.");
}
