using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pottmayer.Pandora.Modules.Finances.Abstractions;
using Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Finances.Domain.ValueObjects;

namespace Pottmayer.Pandora.Modules.Finances.Persistence.EntityConfigs;

internal sealed class CardStatementEntityConfiguration : IEntityTypeConfiguration<CardStatement>
{
    public void Configure(EntityTypeBuilder<CardStatement> builder)
    {
        builder.ToTable("fin007_card_statement", FinancesModule.Schema);

        builder.HasKey(s => s.Id).HasName("pk_fin007");

        builder.Property(s => s.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(s => s.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(s => s.CardId).HasColumnName("card_id").IsRequired();
        builder.Property(s => s.ReferenceMonth).HasColumnName("reference_month").HasMaxLength(7).IsRequired();
        builder.Property(s => s.ClosingDate).HasColumnName("closing_date").IsRequired();
        builder.Property(s => s.DueDate).HasColumnName("due_date").IsRequired();
        builder.Property(s => s.Status)
            .HasColumnName("status")
            .HasConversion(s => s.Value, v => StatementStatus.FromValue(v))
            .HasMaxLength(20)
            .IsRequired();
        builder.Property(s => s.TotalAmount).HasColumnName("total_amount").HasColumnType("numeric(20,8)").IsRequired();
        builder.Property(s => s.PaidAmount).HasColumnName("paid_amount").HasColumnType("numeric(20,8)").IsRequired();
        builder.Property(s => s.ClosedAt).HasColumnName("closed_at");
        builder.Property(s => s.PaidAt).HasColumnName("paid_at");
        builder.Property(s => s.OverdueAt).HasColumnName("overdue_at");

        builder.Property(s => s.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(s => s.CreatedBy).HasColumnName("created_by");
        builder.Property(s => s.UpdatedAt).HasColumnName("updated_at");
        builder.Property(s => s.UpdatedBy).HasColumnName("updated_by");

        builder.HasIndex(s => new { s.UserId, s.Status, s.DueDate }).HasDatabaseName("ix_fin007_user_status_due_date");
        builder.HasIndex(s => new { s.CardId, s.ClosingDate }).HasDatabaseName("ix_fin007_card_closing_date");
    }
}
