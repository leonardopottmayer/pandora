using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pottmayer.Pandora.Modules.Admin.Abstractions;
using Pottmayer.Pandora.Modules.Admin.Domain.Entities;

namespace Pottmayer.Pandora.Modules.Admin.Persistence.EntityConfigs;

internal sealed class FeatureFlagOverrideEntityConfiguration : IEntityTypeConfiguration<FeatureFlagOverride>
{
    public void Configure(EntityTypeBuilder<FeatureFlagOverride> builder)
    {
        builder.ToTable("adm001_feature_flag", AdminModule.Schema);

        builder.HasKey(f => f.Key).HasName("pk_adm001");

        builder.Property(f => f.Key).HasColumnName("key").HasMaxLength(150);
        builder.Property(f => f.Enabled).HasColumnName("enabled").IsRequired();
        builder.Property(f => f.UpdatedBy).HasColumnName("updated_by");
        builder.Property(f => f.UpdatedAt).HasColumnName("updated_at").IsRequired();
    }
}
