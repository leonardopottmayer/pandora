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
}
