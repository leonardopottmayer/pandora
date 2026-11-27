using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pottmayer.Pandora.Modules.Finances.Abstractions;
using Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Finances.Domain.ValueObjects;

namespace Pottmayer.Pandora.Modules.Finances.Persistence.EntityConfigs;

internal sealed class PositionEntityConfiguration : IEntityTypeConfiguration<Position>
{
    public void Configure(EntityTypeBuilder<Position> builder)
    {
        builder.ToTable("fin022_position", FinancesModule.Schema);
        builder.HasKey(x => x.Id).HasName("pk_fin022");

        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(x => x.AccountId).HasColumnName("account_id").IsRequired();
        builder.Property(x => x.AssetId).HasColumnName("asset_id").IsRequired();
        builder.Property(x => x.Behavior).HasColumnName("behavior")
            .HasConversion(b => b.Value, v => AssetBehavior.FromValue(v)).HasMaxLength(20).IsRequired();
        builder.Property(x => x.Status).HasColumnName("status")
            .HasConversion(s => s.Value, v => PositionStatus.FromValue(v)).HasMaxLength(20).IsRequired();
        builder.Property(x => x.Quantity).HasColumnName("quantity").HasPrecision(28, 10);
        builder.Property(x => x.AverageCost).HasColumnName("average_cost").HasPrecision(28, 10);
        builder.Property(x => x.InvestedAmount).HasColumnName("invested_amount").HasPrecision(18, 2);
        builder.Property(x => x.AppliedOn).HasColumnName("applied_on");
        builder.Property(x => x.Index).HasColumnName("index")
            .HasConversion(i => i!.Value, v => FixedIncomeIndex.FromValue(v)).HasMaxLength(20);
        builder.Property(x => x.IndexPercent).HasColumnName("index_percent").HasPrecision(9, 4);
        builder.Property(x => x.SpreadRate).HasColumnName("spread_rate").HasPrecision(9, 4);
        builder.Property(x => x.MaturityOn).HasColumnName("maturity_on");
        builder.Property(x => x.ClosedAt).HasColumnName("closed_at");

        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(x => x.CreatedBy).HasColumnName("created_by");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by");

        builder.HasOne<Account>().WithMany().HasForeignKey(x => x.AccountId).HasConstraintName("fk_fin022_account_id");
        builder.HasOne<Asset>().WithMany().HasForeignKey(x => x.AssetId).HasConstraintName("fk_fin022_asset_id");
    }
}
