namespace Pottmayer.Pandora.Modules.Finances.Application.Auditing;

/// <summary>
/// Entity type and event type identifiers used in <see cref="AuditTrailExtensions.RecordAsync"/>
/// calls, grouped by aggregate so every call site references the same constant instead of
/// retyping the string.
/// </summary>
public static class TransactionEvents
{
    public const string EntityType = "transaction";
    public const string Created = "transaction.created";
    public const string Edited = "transaction.edited";
    public const string Posted = "transaction.posted";
    public const string Voided = "transaction.voided";
    public const string Restored = "transaction.restored";
    public const string Reversed = "transaction.reversed";
}

public static class PendingTransactionEvents
{
    public const string EntityType = "pending-transaction";
    public const string Created = "pending.created";
    public const string Approved = "pending.approved";
    public const string Rejected = "pending.rejected";
    public const string Linked = "pending.linked";
    public const string Edited = "pending.edited";
}

public static class RecurringTransactionEvents
{
    public const string EntityType = "recurring-transaction";
    public const string Created = "recurring.created";
    public const string Updated = "recurring.updated";
    public const string Deleted = "recurring.deleted";
    public const string Paused = "recurring.paused";
    public const string Resumed = "recurring.resumed";
    public const string Finished = "recurring.finished";
    public const string OccurrenceGenerated = "recurring.occurrence-generated";
}

public static class InstallmentPlanEvents
{
    public const string EntityType = "installment-plan";
    public const string Created = "installment-plan.created";
    public const string Voided = "installment-plan.voided";
    public const string Restored = "installment-plan.restored";
}

public static class StatementEvents
{
    public const string EntityType = "statement";
    public const string Created = "statement.created";
    public const string Closed = "statement.closed";
    public const string Reopened = "statement.reopened";
    public const string PaymentReceived = "statement.payment-received";
    public const string Paid = "statement.paid";
    public const string Overdue = "statement.overdue";
    public const string SettledWithoutCash = "statement.settled-without-cash";
}

public static class AccountEvents
{
    public const string EntityType = "account";
    public const string Created = "account.created";
    public const string Updated = "account.updated";
    public const string Deleted = "account.deleted";
    public const string Archived = "account.archived";
    public const string Unarchived = "account.unarchived";
}

public static class CardEvents
{
    public const string EntityType = "card";
    public const string Created = "card.created";
    public const string Updated = "card.updated";
    public const string Deleted = "card.deleted";
    public const string Archived = "card.archived";
    public const string Unarchived = "card.unarchived";
}

public static class UserCategoryEvents
{
    public const string EntityType = "user-category";
    public const string Created = "category.created";
    public const string Updated = "category.updated";
    public const string Activated = "category.activated";
    public const string Deactivated = "category.deactivated";
}

public static class TagEvents
{
    public const string EntityType = "tag";
    public const string Created = "tag.created";
    public const string Updated = "tag.updated";
    public const string Deleted = "tag.deleted";
    public const string Linked = "tag.linked";
    public const string Unlinked = "tag.unlinked";
}
