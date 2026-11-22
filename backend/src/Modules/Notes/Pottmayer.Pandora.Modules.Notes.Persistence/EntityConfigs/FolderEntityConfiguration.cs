using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pottmayer.Pandora.Modules.Notes.Abstractions;
using Pottmayer.Pandora.Modules.Notes.Domain.Aggregates;

namespace Pottmayer.Pandora.Modules.Notes.Persistence.EntityConfigs;

internal sealed class FolderEntityConfiguration : IEntityTypeConfiguration<Folder>
{
    public void Configure(EntityTypeBuilder<Folder> builder)
    {
        builder.ToTable("nte012_folder", NotesModule.Schema);

        builder.HasKey(f => f.Id).HasName("pk_nte012");

        builder.Property(f => f.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(f => f.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(f => f.SpaceId).HasColumnName("space_id").IsRequired();
        builder.Property(f => f.ParentId).HasColumnName("parent_id");
        builder.Property(f => f.Name).HasColumnName("name").HasMaxLength(Folder.MaxNameLength).IsRequired();
        builder.Property(f => f.Icon).HasColumnName("icon").HasMaxLength(50);
        builder.Property(f => f.Description).HasColumnName("description");
        builder.Property(f => f.OrderIndex).HasColumnName("order_index").IsRequired();
        builder.Property(f => f.DeletedAt).HasColumnName("deleted_at");

        builder.Property(f => f.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(f => f.CreatedBy).HasColumnName("created_by");
        builder.Property(f => f.UpdatedAt).HasColumnName("updated_at");
        builder.Property(f => f.UpdatedBy).HasColumnName("updated_by");

        // Declared so EF inserts a space created on the fly before the folder that goes in it.
        builder.HasOne<Space>().WithMany().HasForeignKey(f => f.SpaceId).HasConstraintName("fk_nte012_space");

        builder.HasIndex(f => f.UserId).HasDatabaseName("ix_nte012_user_id");
        builder.HasIndex(f => f.SpaceId).HasDatabaseName("ix_nte012_space_id");
    }
}
