using Pottmayer.Tars.Core.Primitives.Outcomes;

namespace Pottmayer.Pandora.Modules.Finances.Domain.Errors;

public static class TransactionErrors
{
    public static Error NotFound =>
        Error.NotFound("Transactions.NotFound", "Transaction not found.");

    public static Error InvalidKind(string kind) =>
        Error.Validation("Transactions.InvalidKind", $"Transaction kind '{kind}' is not supported.");

    public static Error TransferLegNotAllowed =>
        Error.Validation("Transactions.TransferLegNotAllowed", "Transfer legs are created through the transfer endpoint, not directly.");

    public static Error InvalidAmount =>
        Error.Validation("Transactions.InvalidAmount", "Amount must be greater than zero.");

    public static Error InvalidDescription =>
        Error.Validation("Transactions.InvalidDescription", "Description is required.");

    public static Error KindRequiresInvestmentAccount(string kind) =>
        Error.Validation("Transactions.KindRequiresInvestmentAccount", $"Kind '{kind}' is only allowed on an investment account.");

    public static Error AccountArchived =>
        Error.Conflict("Transactions.AccountArchived", "An archived account cannot receive new transactions.");

    public static Error NotPending =>
        Error.Conflict("Transactions.NotPending", "Only a pending transaction can be posted.");

    public static Error AlreadyVoid =>
        Error.Conflict("Transactions.AlreadyVoid", "The transaction is already voided.");

    public static Error SameAccountTransfer =>
        Error.Validation("Transactions.SameAccountTransfer", "A transfer needs two different accounts.");

    public static Error CrossCurrencyNeedsBothAmounts =>
        Error.Validation("Transactions.CrossCurrencyNeedsBothAmounts", "A transfer between different currencies requires both amounts and an exchange rate.");
}
