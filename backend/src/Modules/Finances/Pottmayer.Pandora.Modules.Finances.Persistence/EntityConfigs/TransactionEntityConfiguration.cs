using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pottmayer.Pandora.Modules.Finances.Abstractions;
using Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Finances.Domain.ValueObjects;

namespace Pottmayer.Pandora.Modules.Finances.Persistence.EntityConfigs;

internal sealed class TransactionEntityConfiguration : IEntityTypeConfiguration<Transaction>
{
    private sealed record SystemDescriptionJson(string Key, IReadOnlyList<string> Args);

    private static string Serialize(SystemDescription v) =>
        JsonSerializer.Serialize(new SystemDescriptionJson(v.Key, v.Args));

    private static SystemDescription Deserialize(string json)
    {
        var dto = JsonSerializer.Deserialize<SystemDescriptionJson>(json);
        return SystemDescription.Create(dto!.Key, dto.Args);
    }

    private static readonly ValueComparer<SystemDescription?> SystemDescriptionComparer = new(
        (a, b) => (a == null && b == null) || (a != null && a.Equals(b)),
        v => v == null ? 0 : v.GetHashCode(),
        v => v);

    public void Configure(EntityTypeBuilder<Transaction> builder)
    {
        builder.ToTable("fin008_transaction", FinancesModule.Schema);

        builder.HasKey(t => t.Id).HasName("pk_fin008");

        builder.Property(t => t.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(t => t.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(t => t.AccountId).HasColumnName("account_id");
        builder.Property(t => t.CardStatementId).HasColumnName("card_statement_id");
        builder.Property(t => t.CardId).HasColumnName("card_id");
        builder.Property(t => t.PaidStatementId).HasColumnName("paid_statement_id");

        builder.Property(t => t.Kind)
               .HasColumnName("kind")
               .HasConversion(k => k.Value, v => TransactionKind.FromValue(v))
               .HasMaxLength(30)
               .IsRequired();

        builder.Property(t => t.Status)
               .HasColumnName("status")
               .HasConversion(s => s.Value, v => TransactionStatus.FromValue(v))
               .HasMaxLength(10)
               .IsRequired();

        builder.Property(t => t.Amount).HasColumnName("amount").HasColumnType("numeric(20,8)").IsRequired();

        builder.Property(t => t.Currency)
               .HasColumnName("currency")
               .HasConversion(c => c.Value, v => CurrencyCode.Create(v))
               .HasMaxLength(10)
               .IsRequired();

        builder.Property(t => t.OccurredOn).HasColumnName("occurred_on").IsRequired();
        builder.Property(t => t.Description).HasColumnName("description").HasMaxLength(255).IsRequired();

        // System-defined descriptor (key + args) persisted as jsonb; null for user-entered text.
        builder.Property(t => t.SystemDescription)
               .HasColumnName("system_description")
               .HasColumnType("jsonb")
               .HasConversion(v => Serialize(v!), v => Deserialize(v), SystemDescriptionComparer);
        builder.Property(t => t.Payee).HasColumnName("payee").HasMaxLength(150);
        builder.Property(t => t.Notes).HasColumnName("notes");
        builder.Property(t => t.SystemCategoryId).HasColumnName("system_category_id");
        builder.Property(t => t.UserCategoryId).HasColumnName("user_category_id");

        builder.Property(t => t.TransferGroupId).HasColumnName("transfer_group_id");
        builder.Property(t => t.FxRate).HasColumnName("fx_rate").HasColumnType("numeric(20,10)");

        builder.Property(t => t.InstallmentPlanId).HasColumnName("installment_plan_id");
        builder.Property(t => t.InstallmentNumber).HasColumnName("installment_number");

        builder.Property(t => t.Origin)
            .HasColumnName("origin")
            .HasConversion(o => o.Value, v => EntryOrigin.FromValue(v))
            .HasMaxLength(15)
            .IsRequired();
        builder.Property(t => t.ReversedTransactionId).HasColumnName("reversed_transaction_id");
        builder.Property(t => t.PendingTransactionId).HasColumnName("pending_transaction_id");
        builder.Property(t => t.RecurringTransactionId).HasColumnName("recurring_transaction_id");

        builder.Property(t => t.PostedAt).HasColumnName("posted_at");
        builder.Property(t => t.VoidedAt).HasColumnName("voided_at");
        builder.Property(t => t.VoidReason).HasColumnName("void_reason").HasMaxLength(255);

        builder.Property(t => t.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(t => t.CreatedBy).HasColumnName("created_by");
        builder.Property(t => t.UpdatedAt).HasColumnName("updated_at");
        builder.Property(t => t.UpdatedBy).HasColumnName("updated_by");

        builder.HasOne<Account>()
            .WithMany()
            .HasForeignKey(t => t.AccountId)
            .HasConstraintName("fk_fin008_account_id");

        builder.HasOne<CardStatement>()
            .WithMany()
            .HasForeignKey(t => t.CardStatementId)
            .HasConstraintName("fk_fin008_card_statement_id");

        builder.HasOne<Card>()
            .WithMany()
            .HasForeignKey(t => t.CardId)
            .HasConstraintName("fk_fin008_card_id");

        builder.HasOne<CardStatement>()
            .WithMany()
            .HasForeignKey(t => t.PaidStatementId)
            .HasConstraintName("fk_fin008_paid_statement_id");

        builder.HasOne<InstallmentPlan>()
            .WithMany()
            .HasForeignKey(t => t.InstallmentPlanId)
            .HasConstraintName("fk_fin008_installment_plan_id");

        builder.HasOne<Transaction>()
            .WithMany()
            .HasForeignKey(t => t.ReversedTransactionId)
            .HasConstraintName("fk_fin008_reversed_transaction_id");

        builder.HasOne<PendingTransaction>()
            .WithMany()
            .HasForeignKey(t => t.PendingTransactionId)
            .HasConstraintName("fk_fin008_pending_transaction_id");

        builder.HasOne<RecurringTransaction>()
            .WithMany()
            .HasForeignKey(t => t.RecurringTransactionId)
            .HasConstraintName("fk_fin008_recurring_transaction_id");

        builder.HasIndex(t => t.ReversedTransactionId)
            .IsUnique()
            .HasDatabaseName("uq_fin008_reversed_transaction_id");

        builder.HasIndex(t => new { t.UserId, t.OccurredOn }).HasDatabaseName("ix_fin008_user_occurred_on");
        builder.HasIndex(t => new { t.AccountId, t.Status, t.OccurredOn }).HasDatabaseName("ix_fin008_account_status_occurred_on");
        builder.HasIndex(t => new { t.CardStatementId, t.Status, t.OccurredOn }).HasDatabaseName("ix_fin008_card_statement_status_occurred_on");
        builder.HasIndex(t => t.PaidStatementId).HasDatabaseName("ix_fin008_paid_statement_id");
        builder.HasIndex(t => new { t.CardId, t.OccurredOn }).HasDatabaseName("ix_fin008_card_id_occurred_on");
        builder.HasIndex(t => t.TransferGroupId).HasDatabaseName("ix_fin008_transfer_group_id");
        builder.HasIndex(t => t.InstallmentPlanId).HasDatabaseName("ix_fin008_installment_plan_id");
        builder.HasIndex(t => t.PendingTransactionId).HasDatabaseName("ix_fin008_pending_transaction_id");
        builder.HasIndex(t => t.RecurringTransactionId).HasDatabaseName("ix_fin008_recurring_transaction_id");
    }
}
