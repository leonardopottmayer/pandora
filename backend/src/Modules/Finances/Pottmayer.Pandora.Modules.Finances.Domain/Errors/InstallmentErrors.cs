using Pottmayer.Tars.Core.Primitives.Outcomes;

namespace Pottmayer.Pandora.Modules.Finances.Domain.Errors;

public static class InstallmentErrors
{
    public static Error NotFound =>
        Error.NotFound("Installments.NotFound", "Installment plan not found.");

    public static Error InvalidCount =>
        Error.Validation("Installments.InvalidCount", "An installment purchase needs at least two installments.");

    public static Error RequiresCard =>
        Error.Validation("Installments.RequiresCard", "Installments are only available for card purchases.");

    public static Error RequiresExpenseKind =>
        Error.Validation("Installments.RequiresExpenseKind", "Only an expense can be split into installments.");

    public static Error InstallmentInClosedStatement =>
        Error.Conflict("Installments.InClosedStatement", "An installment on a closed statement cannot be voided.");

    public static Error NotAnInstallment =>
        Error.Validation("Installments.NotAnInstallment", "This transaction is not part of an installment plan.");
}
