using Pottmayer.Tars.Core.Primitives.Outcomes;

namespace Pottmayer.Pandora.Modules.Finances.Domain.Errors;

public static class PositionErrors
{
    public static Error NotFound =>
        Error.NotFound("Positions.NotFound", "Position not found.");

    public static Error Closed =>
        Error.Conflict("Positions.Closed", "The position is closed.");

    public static Error FirstMovement =>
        Error.Validation("Positions.FirstMovement", "A position starts with an opening or a buy.");

    public static Error OpeningNotFirst =>
        Error.Validation("Positions.OpeningNotFirst", "An opening can only be a position's first movement.");

    public static Error KindNotAllowed(string kind, string behavior) =>
        Error.Validation("Positions.KindNotAllowed", $"A '{kind}' movement does not apply to a '{behavior}' position.");

    public static Error InvalidQuantity =>
        Error.Validation("Positions.InvalidQuantity", "The quantity must be greater than zero and not exceed what the position holds.");

    public static Error InvalidAmount =>
        Error.Validation("Positions.InvalidAmount", "Amounts must be zero or more, and the movement must have a positive value.");

    public static Error InvalidSplitFactor =>
        Error.Validation("Positions.InvalidSplitFactor", "The split factor must be greater than zero and different from one.");

    public static Error IncomeKindRequired =>
        Error.Validation("Positions.IncomeKindRequired", "An income needs its kind.");

    public static Error CurrentValueRequired =>
        Error.Validation("Positions.CurrentValueRequired", "A partial redemption needs the position's current value.");

    public static Error NegativeNet =>
        Error.Validation("Positions.NegativeNet", "Costs and tax cannot exceed the gross amount.");

    public static Error NotLatestMovement =>
        Error.Conflict("Positions.NotLatestMovement", "Only a position's latest movement can be undone.");

    public static Error OpenUnitExists =>
        Error.Conflict("Positions.OpenUnitExists", "This account already has an open position in this asset.");

    public static Error CurrencyMismatch =>
        Error.Validation("Positions.CurrencyMismatch", "The asset's currency must match the account's.");

    public static Error LinkedTransaction =>
        Error.Conflict("Positions.LinkedTransaction", "This entry belongs to an investment movement; change it from the position.");
}
