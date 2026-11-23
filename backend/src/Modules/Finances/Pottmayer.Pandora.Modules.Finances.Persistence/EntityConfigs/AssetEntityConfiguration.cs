using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pottmayer.Pandora.Modules.Finances.Abstractions;
using Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Finances.Domain.ValueObjects;

namespace Pottmayer.Pandora.Modules.Finances.Persistence.EntityConfigs;

internal sealed class AssetEntityConfiguration : IEntityTypeConfiguration<Asset>
{
    public void Configure(EntityTypeBuilder<Asset> builder)
    {
        builder.ToTable("fin021_asset", FinancesModule.Schema);
        builder.HasKey(x => x.Id).HasName("pk_fin021");

        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(x => x.AssetClassId).HasColumnName("asset_class_id").IsRequired();
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(100).IsRequired();
        builder.Property(x => x.Code).HasColumnName("code").HasMaxLength(20);
        builder.Property(x => x.Isin).HasColumnName("isin").HasMaxLength(12);
        builder.Property(x => x.Issuer).HasColumnName("issuer").HasMaxLength(100);
        builder.Property(x => x.Currency).HasColumnName("currency")
            .HasConversion(c => c.Value, v => CurrencyCode.Create(v)).HasMaxLength(10).IsRequired();
        builder.Property(x => x.ArchivedAt).HasColumnName("archived_at");

        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(x => x.CreatedBy).HasColumnName("created_by");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by");

        builder.HasOne<AssetClass>().WithMany().HasForeignKey(x => x.AssetClassId).HasConstraintName("fk_fin021_asset_class_id");
    }
}
