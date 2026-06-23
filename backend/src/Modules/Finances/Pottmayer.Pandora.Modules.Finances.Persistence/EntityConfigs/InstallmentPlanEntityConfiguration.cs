using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pottmayer.Pandora.Modules.Finances.Abstractions;
using Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Finances.Domain.ValueObjects;

namespace Pottmayer.Pandora.Modules.Finances.Persistence.EntityConfigs;

internal sealed class InstallmentPlanEntityConfiguration : IEntityTypeConfiguration<InstallmentPlan>
{
    public void Configure(EntityTypeBuilder<InstallmentPlan> builder)
    {
        builder.ToTable("fin009_installment_plan", FinancesModule.Schema);

        builder.HasKey(p => p.Id).HasName("pk_fin009");

        builder.Property(p => p.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(p => p.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(p => p.CardId).HasColumnName("card_id").IsRequired();
        builder.Property(p => p.Origin)
            .HasColumnName("origin")
            .HasConversion(o => o.Value, v => EntryOrigin.FromValue(v))
            .HasMaxLength(10)
            .IsRequired();
        builder.Property(p => p.TotalAmount).HasColumnName("total_amount").HasColumnType("numeric(20,8)").IsRequired();
        builder.Property(p => p.TotalIsEstimate).HasColumnName("total_is_estimate").IsRequired();
        builder.Property(p => p.InstallmentCount).HasColumnName("installment_count").IsRequired();
        builder.Property(p => p.FirstReferenceMonth).HasColumnName("first_reference_month").HasMaxLength(7).IsRequired();
        builder.Property(p => p.Description).HasColumnName("description").HasMaxLength(255).IsRequired();
        builder.Property(p => p.NormalizedDescription).HasColumnName("normalized_description").HasMaxLength(255).IsRequired();

        builder.Property(p => p.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(p => p.CreatedBy).HasColumnName("created_by");
        builder.Property(p => p.UpdatedAt).HasColumnName("updated_at");
        builder.Property(p => p.UpdatedBy).HasColumnName("updated_by");

        builder.HasOne<Card>()
            .WithMany()
            .HasForeignKey(p => p.CardId)
            .HasConstraintName("fk_fin009_card_id");

        builder.HasIndex(p => p.UserId).HasDatabaseName("ix_fin009_user_id");
        builder.HasIndex(p => new { p.CardId, p.NormalizedDescription }).HasDatabaseName("ix_fin009_card_normalized_description");
    }
}
