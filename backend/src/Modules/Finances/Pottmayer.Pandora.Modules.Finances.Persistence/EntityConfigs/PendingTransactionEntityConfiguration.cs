using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pottmayer.Pandora.Modules.Finances.Abstractions;
using Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;

namespace Pottmayer.Pandora.Modules.Finances.Persistence.EntityConfigs;

internal sealed class PendingTransactionEntityConfiguration : IEntityTypeConfiguration<PendingTransaction>
{
    public void Configure(EntityTypeBuilder<PendingTransaction> builder)
    {
        builder.ToTable("fin011_pending_transaction", FinancesModule.Schema);

        builder.HasKey(p => p.Id).HasName("pk_fin011");

        builder.Property(p => p.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(p => p.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(p => p.Source).HasColumnName("source").HasMaxLength(15).IsRequired();
        builder.Property(p => p.RecurringTransactionId).HasColumnName("recurring_transaction_id");
        builder.Property(p => p.ImportRowId).HasColumnName("import_row_id");
        builder.Property(p => p.DuplicateOfTransactionId).HasColumnName("duplicate_of_transaction_id");
        builder.Property(p => p.DuplicateOfPendingId).HasColumnName("duplicate_of_pending_id");
        builder.Property(p => p.DedupStatus).HasColumnName("dedup_status").HasMaxLength(15);
        builder.Property(p => p.InstallmentNumber).HasColumnName("installment_number");
        builder.Property(p => p.InstallmentCount).HasColumnName("installment_count");
        builder.Property(p => p.MatchedInstallmentPlanId).HasColumnName("matched_installment_plan_id");

        // payload
        builder.Property(p => p.AccountId).HasColumnName("account_id");
        builder.Property(p => p.CardId).HasColumnName("card_id");
        builder.Property(p => p.Kind).HasColumnName("kind").HasMaxLength(30).IsRequired();
        builder.Property(p => p.Amount).HasColumnName("amount").HasColumnType("numeric(20,8)");
        builder.Property(p => p.Currency).HasColumnName("currency").HasMaxLength(10).IsRequired();
        builder.Property(p => p.OccurredOn).HasColumnName("occurred_on").IsRequired();
        builder.Property(p => p.Description).HasColumnName("description").HasMaxLength(255).IsRequired();
        builder.Property(p => p.Payee).HasColumnName("payee").HasMaxLength(150);
        builder.Property(p => p.Notes).HasColumnName("notes");
        builder.Property(p => p.SystemCategoryId).HasColumnName("system_category_id");
        builder.Property(p => p.UserCategoryId).HasColumnName("user_category_id");
        builder.Property(p => p.SuggestedStatementId).HasColumnName("suggested_statement_id");

        builder.Property(p => p.OriginalPayload).HasColumnName("original_payload").HasColumnType("jsonb").IsRequired();

        // decision
        builder.Property(p => p.Status).HasColumnName("status").HasMaxLength(10).IsRequired();
        builder.Property(p => p.DecidedAt).HasColumnName("decided_at");
        builder.Property(p => p.DecidedBy).HasColumnName("decided_by");
        builder.Property(p => p.RejectionReason).HasColumnName("rejection_reason").HasMaxLength(255);
        builder.Property(p => p.TransactionId).HasColumnName("transaction_id");

        builder.Property(p => p.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(p => p.CreatedBy).HasColumnName("created_by");
        builder.Property(p => p.UpdatedAt).HasColumnName("updated_at");
        builder.Property(p => p.UpdatedBy).HasColumnName("updated_by");

        builder.HasOne<RecurringTransaction>()
            .WithMany()
            .HasForeignKey(p => p.RecurringTransactionId)
            .HasConstraintName("fk_fin011_recurring_transaction_id");

        builder.HasOne<CardStatement>()
            .WithMany()
            .HasForeignKey(p => p.SuggestedStatementId)
            .HasConstraintName("fk_fin011_suggested_statement_id");

        builder.HasOne<Transaction>()
            .WithMany()
            .HasForeignKey(p => p.TransactionId)
            .HasConstraintName("fk_fin011_transaction_id");

        builder.HasIndex(p => new { p.UserId, p.Status }).HasDatabaseName("ix_fin011_user_status");
        builder.HasIndex(p => p.RecurringTransactionId).HasDatabaseName("ix_fin011_recurring_transaction_id");

        builder.HasIndex(p => new { p.RecurringTransactionId, p.OccurredOn })
            .IsUnique()
            .HasFilter("recurring_transaction_id IS NOT NULL")
            .HasDatabaseName("uq_fin011_recurrence_occurrence");
    }
}
