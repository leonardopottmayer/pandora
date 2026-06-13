using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pottmayer.Pandora.Modules.Finances.Abstractions;
using Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Finances.Domain.ValueObjects;

namespace Pottmayer.Pandora.Modules.Finances.Persistence.EntityConfigs;

internal sealed class TagLinkEntityConfiguration : IEntityTypeConfiguration<TagLink>
{
    public void Configure(EntityTypeBuilder<TagLink> builder)
    {
        builder.ToTable("fin005_tag_link", FinancesModule.Schema);

        builder.HasKey(l => l.Id).HasName("pk_fin005");

        builder.Property(l => l.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(l => l.TagId).HasColumnName("tag_id").IsRequired();

        builder.Property(l => l.EntityType)
               .HasColumnName("entity_type")
               .HasConversion(t => t.Value, v => TaggableEntityType.FromValue(v))
               .HasMaxLength(30)
               .IsRequired();

        builder.Property(l => l.EntityId).HasColumnName("entity_id").IsRequired();

        builder.Property(l => l.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(l => l.CreatedBy).HasColumnName("created_by");
        builder.Property(l => l.UpdatedAt).HasColumnName("updated_at");
        builder.Property(l => l.UpdatedBy).HasColumnName("updated_by");

        builder.HasIndex(l => new { l.EntityType, l.EntityId }).HasDatabaseName("ix_fin005_entity");
        builder.HasIndex(l => l.TagId).HasDatabaseName("ix_fin005_tag_id");
    }
}
