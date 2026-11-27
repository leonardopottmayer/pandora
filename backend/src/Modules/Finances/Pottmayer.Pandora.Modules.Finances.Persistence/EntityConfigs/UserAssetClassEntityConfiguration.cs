using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pottmayer.Pandora.Modules.Finances.Abstractions;
using Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Finances.Domain.ValueObjects;

namespace Pottmayer.Pandora.Modules.Finances.Persistence.EntityConfigs;

internal sealed class UserAssetClassEntityConfiguration : IEntityTypeConfiguration<UserAssetClass>
{
    public void Configure(EntityTypeBuilder<UserAssetClass> builder)
    {
        builder.ToTable("fin025_user_asset_class", FinancesModule.Schema);
        builder.HasKey(x => x.Id).HasName("pk_fin025");

        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(60).IsRequired();
        builder.Property(x => x.Behavior).HasColumnName("behavior")
            .HasConversion(b => b.Value, v => AssetBehavior.FromValue(v)).HasMaxLength(20).IsRequired();
        builder.Property(x => x.ArchivedAt).HasColumnName("archived_at");

        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(x => x.CreatedBy).HasColumnName("created_by");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by");
    }
}
