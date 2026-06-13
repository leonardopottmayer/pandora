using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pottmayer.Pandora.Modules.Finances.Abstractions;
using Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Finances.Domain.ValueObjects;

namespace Pottmayer.Pandora.Modules.Finances.Persistence.EntityConfigs;

internal sealed class TransactionEntityConfiguration : IEntityTypeConfiguration<Transaction>
{
    public void Configure(EntityTypeBuilder<Transaction> builder)
    {
        builder.ToTable("fin008_transaction", FinancesModule.Schema);

        builder.HasKey(t => t.Id).HasName("pk_fin008");

        builder.Property(t => t.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(t => t.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(t => t.AccountId).HasColumnName("account_id").IsRequired();

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
        builder.Property(t => t.Payee).HasColumnName("payee").HasMaxLength(150);
        builder.Property(t => t.Notes).HasColumnName("notes");
        builder.Property(t => t.SystemCategoryId).HasColumnName("system_category_id");
        builder.Property(t => t.UserCategoryId).HasColumnName("user_category_id");

        builder.Property(t => t.TransferGroupId).HasColumnName("transfer_group_id");
        builder.Property(t => t.FxRate).HasColumnName("fx_rate").HasColumnType("numeric(20,10)");

        builder.Property(t => t.Origin).HasColumnName("origin").HasMaxLength(15).IsRequired();

        builder.Property(t => t.PostedAt).HasColumnName("posted_at");
        builder.Property(t => t.VoidedAt).HasColumnName("voided_at");
        builder.Property(t => t.VoidReason).HasColumnName("void_reason").HasMaxLength(255);

        builder.Property(t => t.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(t => t.CreatedBy).HasColumnName("created_by");
        builder.Property(t => t.UpdatedAt).HasColumnName("updated_at");
        builder.Property(t => t.UpdatedBy).HasColumnName("updated_by");

        builder.HasIndex(t => new { t.UserId, t.OccurredOn }).HasDatabaseName("ix_fin008_user_occurred_on");
        builder.HasIndex(t => new { t.AccountId, t.Status, t.OccurredOn }).HasDatabaseName("ix_fin008_account_status_occurred_on");
        builder.HasIndex(t => t.TransferGroupId).HasDatabaseName("ix_fin008_transfer_group_id");
    }
}
