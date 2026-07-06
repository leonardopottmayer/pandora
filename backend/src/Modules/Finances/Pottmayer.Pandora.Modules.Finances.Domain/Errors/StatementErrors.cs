using Pottmayer.Tars.Core.Primitives.Outcomes;

namespace Pottmayer.Pandora.Modules.Finances.Domain.Errors;

public static class StatementErrors
{
    public static Error NotFound =>
        Error.NotFound("Statements.NotFound", "Statement not found.");

    public static Error InvalidTarget =>
        Error.Validation("Statements.InvalidTarget", "Transaction target must be either an account or a card statement.");

    public static Error ForcedStatementDoesNotBelongToCard =>
        Error.Validation("Statements.ForcedStatementDoesNotBelongToCard", "The forced statement does not belong to the selected card.");

    public static Error StatementClosed =>
        Error.Conflict("Statements.Closed", "The statement is closed to new purchases.");

    public static Error InvalidPaymentAmount =>
        Error.Validation("Statements.InvalidPaymentAmount", "Payment amount must be greater than zero.");

    public static Error CannotPayWithArchivedAccount =>
        Error.Conflict("Statements.CannotPayWithArchivedAccount", "An archived account cannot pay a statement.");

    public static Error MissingFxRate =>
        Error.Validation("Statements.MissingFxRate", "An exchange rate is required when paying a statement from an account with another currency.");

    public static Error AlreadyOpen =>
        Error.Conflict("Statements.AlreadyOpen", "The statement is already open.");

    public static Error AlreadyPaid =>
        Error.Conflict("Statements.AlreadyPaid", "The statement is fully paid. Void or reverse the payment before reopening.");

    public static Error NothingToSettle =>
        Error.Conflict("Statements.NothingToSettle", "The statement has no outstanding balance to settle.");
}
