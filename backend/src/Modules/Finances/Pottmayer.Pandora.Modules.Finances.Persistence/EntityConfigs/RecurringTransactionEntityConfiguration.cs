using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pottmayer.Pandora.Modules.Finances.Abstractions;
using Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Finances.Domain.ValueObjects;

namespace Pottmayer.Pandora.Modules.Finances.Persistence.EntityConfigs;

internal sealed class RecurringTransactionEntityConfiguration : IEntityTypeConfiguration<RecurringTransaction>
{
    public void Configure(EntityTypeBuilder<RecurringTransaction> builder)
    {
        builder.ToTable("fin010_recurring_transaction", FinancesModule.Schema);

        builder.HasKey(r => r.Id).HasName("pk_fin010");

        builder.Property(r => r.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(r => r.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(r => r.Name).HasColumnName("name").HasMaxLength(100).IsRequired();

        // template
        builder.Property(r => r.AccountId).HasColumnName("account_id");
        builder.Property(r => r.CardId).HasColumnName("card_id");
        builder.Property(r => r.Kind).HasColumnName("kind").HasMaxLength(30).IsRequired();
        builder.Property(r => r.Amount).HasColumnName("amount").HasColumnType("numeric(20,8)");
        builder.Property(r => r.AmountIsEstimate).HasColumnName("amount_is_estimate").IsRequired();
        builder.Property(r => r.Description).HasColumnName("description").HasMaxLength(255).IsRequired();
        builder.Property(r => r.Payee).HasColumnName("payee").HasMaxLength(150);
        builder.Property(r => r.SystemCategoryId).HasColumnName("system_category_id");
        builder.Property(r => r.UserCategoryId).HasColumnName("user_category_id");

        // rule
        builder.Property(r => r.Frequency)
            .HasColumnName("frequency")
            .HasConversion(f => f.Value, v => RecurrenceFrequency.FromValue(v))
            .HasMaxLength(10)
            .IsRequired();
        builder.Property(r => r.Interval).HasColumnName("interval").IsRequired();
        builder.Property(r => r.DayOfMonth).HasColumnName("day_of_month");
        builder.Property(r => r.Weekday).HasColumnName("weekday");
        builder.Property(r => r.StartDate).HasColumnName("start_date").IsRequired();
        builder.Property(r => r.EndDate).HasColumnName("end_date");
        builder.Property(r => r.MaxOccurrences).HasColumnName("max_occurrences");

        // execution
        builder.Property(r => r.Status)
            .HasColumnName("status")
            .HasConversion(s => s.Value, v => RecurringTransactionStatus.FromValue(v))
            .HasMaxLength(10)
            .IsRequired();
        builder.Property(r => r.AutoPost).HasColumnName("auto_post").IsRequired();
        builder.Property(r => r.AutoGenerate).HasColumnName("auto_generate").IsRequired();
        builder.Property(r => r.NextOccurrenceOn).HasColumnName("next_occurrence_on").IsRequired();
        builder.Property(r => r.OccurrencesCount).HasColumnName("occurrences_count").IsRequired();

        builder.Property(r => r.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(r => r.CreatedBy).HasColumnName("created_by");
        builder.Property(r => r.UpdatedAt).HasColumnName("updated_at");
        builder.Property(r => r.UpdatedBy).HasColumnName("updated_by");

        builder.HasOne<Account>()
            .WithMany()
            .HasForeignKey(r => r.AccountId)
            .HasConstraintName("fk_fin010_account_id");

        builder.HasOne<Card>()
            .WithMany()
            .HasForeignKey(r => r.CardId)
            .HasConstraintName("fk_fin010_card_id");

        builder.HasIndex(r => r.UserId).HasDatabaseName("ix_fin010_user_id");
        builder.HasIndex(r => new { r.Status, r.NextOccurrenceOn }).HasDatabaseName("ix_fin010_status_next_occurrence");
    }
}
