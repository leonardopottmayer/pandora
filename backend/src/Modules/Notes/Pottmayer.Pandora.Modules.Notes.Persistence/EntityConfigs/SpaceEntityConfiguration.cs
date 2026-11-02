using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pottmayer.Pandora.Modules.Notes.Abstractions;
using Pottmayer.Pandora.Modules.Notes.Domain.Aggregates;

namespace Pottmayer.Pandora.Modules.Notes.Persistence.EntityConfigs;

internal sealed class SpaceEntityConfiguration : IEntityTypeConfiguration<Space>
{
    public void Configure(EntityTypeBuilder<Space> builder)
    {
        builder.ToTable("nte007_space", NotesModule.Schema);

        builder.HasKey(s => s.Id).HasName("pk_nte007");

        builder.Property(s => s.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(s => s.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(s => s.Name).HasColumnName("name").HasMaxLength(100).IsRequired();
        builder.Property(s => s.Icon).HasColumnName("icon").HasMaxLength(50);
        builder.Property(s => s.Color).HasColumnName("color").HasMaxLength(20);
        builder.Property(s => s.Description).HasColumnName("description").HasMaxLength(500);
        builder.Property(s => s.IsDefault).HasColumnName("is_default").IsRequired();
        builder.Property(s => s.ArchivedAt).HasColumnName("archived_at");
        builder.Property(s => s.DeletedAt).HasColumnName("deleted_at");

        builder.Property(s => s.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(s => s.CreatedBy).HasColumnName("created_by");
        builder.Property(s => s.UpdatedAt).HasColumnName("updated_at");
        builder.Property(s => s.UpdatedBy).HasColumnName("updated_by");

        builder.HasIndex(s => s.UserId).HasDatabaseName("ix_nte007_user_id");
        builder.HasIndex(s => s.UserId)
               .HasDatabaseName("uq_nte007_user_default")
               .IsUnique()
               .HasFilter("is_default AND deleted_at IS NULL");
    }
}
