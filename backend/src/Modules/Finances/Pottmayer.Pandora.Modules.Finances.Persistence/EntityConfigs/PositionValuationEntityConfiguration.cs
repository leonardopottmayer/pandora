using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pottmayer.Pandora.Modules.Finances.Abstractions;
using Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Finances.Domain.ValueObjects;

namespace Pottmayer.Pandora.Modules.Finances.Persistence.EntityConfigs;

internal sealed class PositionValuationEntityConfiguration : IEntityTypeConfiguration<PositionValuation>
{
    public void Configure(EntityTypeBuilder<PositionValuation> builder)
    {
        builder.ToTable("fin024_position_valuation", FinancesModule.Schema);
        builder.HasKey(x => x.Id).HasName("pk_fin024");

        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(x => x.PositionId).HasColumnName("position_id").IsRequired();
        builder.Property(x => x.ValuedOn).HasColumnName("valued_on").IsRequired();
        builder.Property(x => x.Value).HasColumnName("value").HasPrecision(18, 2).IsRequired();
        builder.Property(x => x.Source).HasColumnName("source")
            .HasConversion(s => s.Value, v => ValuationSource.FromValue(v)).HasMaxLength(20).IsRequired();

        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(x => x.CreatedBy).HasColumnName("created_by");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by");

        builder.HasOne<Position>().WithMany().HasForeignKey(x => x.PositionId).HasConstraintName("fk_fin024_position_id");
    }
}
