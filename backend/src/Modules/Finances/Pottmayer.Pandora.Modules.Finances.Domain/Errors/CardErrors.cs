using Pottmayer.Tars.Core.Primitives.Outcomes;

namespace Pottmayer.Pandora.Modules.Finances.Domain.Errors;

public static class CardErrors
{
    public static Error NotFound =>
        Error.NotFound("Cards.NotFound", "Card not found.");

    public static Error InvalidName =>
        Error.Validation("Cards.InvalidName", "Card name is required.");

    public static Error NameAlreadyExists =>
        Error.Conflict("Cards.NameAlreadyExists", "A card with this name already exists.");

    public static Error InvalidCurrency(string currency) =>
        Error.Validation("Cards.InvalidCurrency", $"Currency '{currency}' is invalid.");

    public static Error InvalidClosingDay =>
        Error.Validation("Cards.InvalidClosingDay", "Closing day must be between 1 and 28.");

    public static Error InvalidDueDay =>
        Error.Validation("Cards.InvalidDueDay", "Due day must be between 1 and 28.");

    public static Error InvalidLastFour =>
        Error.Validation("Cards.InvalidLastFour", "Card last four must contain exactly 4 digits.");

    public static Error InvalidCreditLimit =>
        Error.Validation("Cards.InvalidCreditLimit", "Credit limit cannot be negative.");

    public static Error Archived =>
        Error.Conflict("Cards.Archived", "An archived card cannot receive new purchases or mutations.");

    public static Error DefaultPaymentAccountNotFound =>
        Error.Validation("Cards.DefaultPaymentAccountNotFound", "Default payment account was not found.");
}
