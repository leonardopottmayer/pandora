using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pottmayer.Pandora.Modules.Finances.Abstractions;
using Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Finances.Domain.ValueObjects;

namespace Pottmayer.Pandora.Modules.Finances.Persistence.EntityConfigs;

internal sealed class PositionMovementEntityConfiguration : IEntityTypeConfiguration<PositionMovement>
{
    public void Configure(EntityTypeBuilder<PositionMovement> builder)
    {
        builder.ToTable("fin023_position_movement", FinancesModule.Schema);
        builder.HasKey(x => x.Id).HasName("pk_fin023");

        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(x => x.PositionId).HasColumnName("position_id").IsRequired();
        builder.Property(x => x.Kind).HasColumnName("kind")
            .HasConversion(k => k.Value, v => PositionMovementKind.FromValue(v)).HasMaxLength(20).IsRequired();
        builder.Property(x => x.OccurredOn).HasColumnName("occurred_on").IsRequired();
        builder.Property(x => x.Quantity).HasColumnName("quantity").HasPrecision(28, 10);
        builder.Property(x => x.UnitPrice).HasColumnName("unit_price").HasPrecision(28, 10);
        builder.Property(x => x.GrossAmount).HasColumnName("gross_amount").HasPrecision(18, 2);
        builder.Property(x => x.Costs).HasColumnName("costs").HasPrecision(18, 2).IsRequired();
        builder.Property(x => x.TaxWithheld).HasColumnName("tax_withheld").HasPrecision(18, 2).IsRequired();
        builder.Property(x => x.NetAmount).HasColumnName("net_amount").HasPrecision(18, 2);
        builder.Property(x => x.IncomeKind).HasColumnName("income_kind")
            .HasConversion(k => k!.Value, v => IncomeKind.FromValue(v)).HasMaxLength(30);
        builder.Property(x => x.SplitFactor).HasColumnName("split_factor").HasPrecision(18, 8);
        builder.Property(x => x.RealizedResult).HasColumnName("realized_result").HasPrecision(18, 2);
        builder.Property(x => x.QuantityBefore).HasColumnName("quantity_before").HasPrecision(28, 10);
        builder.Property(x => x.AverageCostBefore).HasColumnName("average_cost_before").HasPrecision(28, 10);
        builder.Property(x => x.InvestedAmountBefore).HasColumnName("invested_amount_before").HasPrecision(18, 2);
        builder.Property(x => x.TransactionId).HasColumnName("transaction_id");
        builder.Property(x => x.Notes).HasColumnName("notes").HasMaxLength(255);

        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(x => x.CreatedBy).HasColumnName("created_by");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by");

        builder.Ignore(x => x.LedgerKind);

        builder.HasOne<Position>().WithMany().HasForeignKey(x => x.PositionId).HasConstraintName("fk_fin023_position_id");
        builder.HasOne<Transaction>().WithMany().HasForeignKey(x => x.TransactionId).HasConstraintName("fk_fin023_transaction_id");
    }
}
