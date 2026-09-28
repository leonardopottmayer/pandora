using Pottmayer.Tars.Core.Primitives.Outcomes;

namespace Pottmayer.Pandora.Modules.Finances.Domain.Errors;

public static class PendingTransactionErrors
{
    public static Error NotFound =>
        Error.NotFound("PendingTransactions.NotFound", "Pending transaction not found.");

    public static Error AlreadyDecided =>
        Error.Conflict("PendingTransactions.AlreadyDecided", "Pending transaction has already been approved or rejected.");

    public static Error MissingAmount =>
        Error.Validation("PendingTransactions.MissingAmount", "Amount is required to approve this pending transaction.");

    public static Error NotImportSource =>
        Error.Validation("PendingTransactions.NotImportSource", "Only import suggestions can be linked to an existing transaction.");

    public static Error NotAccountTarget =>
        Error.Validation("PendingTransactions.NotAccountTarget", "Only account suggestions can be turned into a transfer.");

    public static Error InvalidTransferDirections =>
        Error.Validation("PendingTransactions.InvalidTransferDirections", "A transfer needs exactly one inflow and one outflow suggestion.");

    public static Error SameTransferAccount =>
        Error.Validation("PendingTransactions.SameTransferAccount", "A transfer needs two different accounts.");

    public static Error SingleTargetRequired =>
        Error.Validation("PendingTransactions.SingleTargetRequired", "Choose exactly one account or card.");

    public static Error InvalidAmount =>
        Error.Validation("PendingTransactions.InvalidAmount", "The amount must be positive.");

    public static Error DescriptionRequired =>
        Error.Validation("PendingTransactions.DescriptionRequired", "A description is required.");
}
