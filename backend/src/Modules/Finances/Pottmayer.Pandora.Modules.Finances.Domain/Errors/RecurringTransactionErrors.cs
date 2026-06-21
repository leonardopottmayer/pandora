using Pottmayer.Tars.Core.Primitives.Outcomes;

namespace Pottmayer.Pandora.Modules.Finances.Domain.Errors;

public static class RecurringTransactionErrors
{
    public static Error NotFound =>
        Error.NotFound("RecurringTransactions.NotFound", "Recurring transaction not found.");

    public static Error AlreadyPaused =>
        Error.Conflict("RecurringTransactions.AlreadyPaused", "Recurring transaction is already paused.");

    public static Error AlreadyActive =>
        Error.Conflict("RecurringTransactions.AlreadyActive", "Recurring transaction is already active.");

    public static Error Finished =>
        Error.Conflict("RecurringTransactions.Finished", "Recurring transaction has finished and cannot be modified.");

    public static Error InvalidFrequency(string? value) =>
        Error.Validation("RecurringTransactions.InvalidFrequency", $"Frequency '{value}' is not supported; use daily, weekly, monthly or yearly.");

    public static Error InvalidInterval =>
        Error.Validation("RecurringTransactions.InvalidInterval", "Interval must be at least 1.");

    public static Error InvalidDayOfMonth =>
        Error.Validation("RecurringTransactions.InvalidDayOfMonth", "Day of month must be between 1 and 31.");

    public static Error InvalidWeekday =>
        Error.Validation("RecurringTransactions.InvalidWeekday", "Weekday must be between 0 (Monday) and 6 (Sunday).");

    public static Error EndDateBeforeStart =>
        Error.Validation("RecurringTransactions.EndDateBeforeStart", "End date must be after start date.");

    public static Error AutoPostRequiresAmount =>
        Error.Validation("RecurringTransactions.AutoPostRequiresAmount", "Auto-post requires a fixed amount (not variable).");

    public static Error MissingName =>
        Error.Validation("RecurringTransactions.MissingName", "Name is required.");

    public static Error MissingDescription =>
        Error.Validation("RecurringTransactions.MissingDescription", "Description is required.");

    public static Error InvalidDestination =>
        Error.Validation("RecurringTransactions.InvalidDestination", "Destination must be 'inbox' or 'transactions'.");

    public static Error ManualGenerationRequiresAmount =>
        Error.Validation("RecurringTransactions.ManualGenerationRequiresAmount", "Posting directly to transactions requires an amount.");

    public static Error OccurrenceAlreadyInInbox =>
        Error.Conflict("RecurringTransactions.OccurrenceAlreadyInInbox", "An inbox suggestion already exists for this recurrence and date.");
}
