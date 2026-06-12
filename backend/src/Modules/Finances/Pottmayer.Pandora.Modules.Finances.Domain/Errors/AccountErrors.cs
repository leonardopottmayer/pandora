using Pottmayer.Tars.Core.Primitives.Outcomes;

namespace Pottmayer.Pandora.Modules.Finances.Domain.Errors;

public static class AccountErrors
{
    public static Error NotFound =>
        Error.NotFound("Accounts.NotFound", "Account not found.");

    public static Error InvalidName =>
        Error.Validation("Accounts.InvalidName", "Account name is required.");

    public static Error InvalidType(string type) =>
        Error.Validation("Accounts.InvalidType", $"Account type '{type}' is not supported.");

    public static Error InvalidCurrency(string currency) =>
        Error.Validation("Accounts.InvalidCurrency", $"Currency '{currency}' is not a valid code.");

    public static Error NameAlreadyExists =>
        Error.Conflict("Accounts.NameAlreadyExists", "An account with this name already exists.");

    public static Error Archived =>
        Error.Conflict("Accounts.Archived", "An archived account cannot be edited; unarchive it first.");
}
