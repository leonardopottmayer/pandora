using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pottmayer.Pandora.Modules.Finances.Abstractions;
using Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Finances.Domain.ValueObjects;

namespace Pottmayer.Pandora.Modules.Finances.Persistence.EntityConfigs;

internal sealed class SystemAssetClassEntityConfiguration : IEntityTypeConfiguration<SystemAssetClass>
{
    public void Configure(EntityTypeBuilder<SystemAssetClass> builder)
    {
        builder.ToTable("fin020_system_asset_class", FinancesModule.Schema);
        builder.HasKey(x => x.Id).HasName("pk_fin020");

        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.Code).HasColumnName("code").HasMaxLength(40).IsRequired();
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(60).IsRequired();
        builder.Property(x => x.Behavior).HasColumnName("behavior")
            .HasConversion(b => b.Value, v => AssetBehavior.FromValue(v)).HasMaxLength(20).IsRequired();
        builder.Property(x => x.DisplayOrder).HasColumnName("display_order").IsRequired();
    }
}
