using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pottmayer.Pandora.Modules.Notes.Abstractions;
using Pottmayer.Pandora.Modules.Notes.Domain.Aggregates;

namespace Pottmayer.Pandora.Modules.Notes.Persistence.EntityConfigs;

internal sealed class MediaItemEntityConfiguration : IEntityTypeConfiguration<MediaItem>
{
    public void Configure(EntityTypeBuilder<MediaItem> builder)
    {
        builder.ToTable("nte002_media", NotesModule.Schema);
        builder.HasKey(m => m.Id).HasName("pk_nte002");

        builder.Property(m => m.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(m => m.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(m => m.FolderId).HasColumnName("folder_id");
        builder.Property(m => m.Kind).HasColumnName("kind").HasMaxLength(20).IsRequired();
        builder.Property(m => m.Name).HasColumnName("name").HasMaxLength(255).IsRequired();
        builder.Property(m => m.OriginalFileName).HasColumnName("original_file_name").HasMaxLength(255).IsRequired();
        builder.Property(m => m.ContentType).HasColumnName("content_type").HasMaxLength(255).IsRequired();
        builder.Property(m => m.SizeBytes).HasColumnName("size_bytes").IsRequired();
        builder.Property(m => m.Width).HasColumnName("width");
        builder.Property(m => m.Height).HasColumnName("height");
        builder.Property(m => m.StorageBackend).HasColumnName("storage_backend").HasMaxLength(50).IsRequired();
        builder.Property(m => m.StorageKey).HasColumnName("storage_key").HasMaxLength(1024).IsRequired();
        builder.Property(m => m.DeletedAt).HasColumnName("deleted_at");

        builder.Property(m => m.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(m => m.CreatedBy).HasColumnName("created_by");
        builder.Property(m => m.UpdatedAt).HasColumnName("updated_at");
        builder.Property(m => m.UpdatedBy).HasColumnName("updated_by");

        builder.HasIndex(m => m.UserId).HasDatabaseName("ix_nte002_user_id");
        builder.HasIndex(m => m.FolderId).HasDatabaseName("ix_nte002_folder_id");

        // Declared so EF orders the writes when a folder's items move out before the folder goes.
        builder.HasOne<MediaFolder>().WithMany().HasForeignKey(m => m.FolderId).HasConstraintName("fk_nte002_folder");
    }
}

internal sealed class MediaFolderEntityConfiguration : IEntityTypeConfiguration<MediaFolder>
{
    public void Configure(EntityTypeBuilder<MediaFolder> builder)
    {
        builder.ToTable("nte008_media_folder", NotesModule.Schema);
        builder.HasKey(f => f.Id).HasName("pk_nte008");

        builder.Property(f => f.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(f => f.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(f => f.ParentId).HasColumnName("parent_id");
        builder.Property(f => f.Name).HasColumnName("name").HasMaxLength(100).IsRequired();

        builder.Property(f => f.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(f => f.CreatedBy).HasColumnName("created_by");
        builder.Property(f => f.UpdatedAt).HasColumnName("updated_at");
        builder.Property(f => f.UpdatedBy).HasColumnName("updated_by");

        builder.HasIndex(f => f.UserId).HasDatabaseName("ix_nte008_user_id");
        builder.HasOne<MediaFolder>().WithMany().HasForeignKey(f => f.ParentId).HasConstraintName("fk_nte008_parent");
    }
}

internal sealed class PageMediaEntityConfiguration : IEntityTypeConfiguration<PageMedia>
{
    public void Configure(EntityTypeBuilder<PageMedia> builder)
    {
        builder.ToTable("nte010_page_media", NotesModule.Schema);
        builder.HasKey(r => r.Id).HasName("pk_nte010");

        builder.Property(r => r.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(r => r.PageId).HasColumnName("page_id").IsRequired();
        builder.Property(r => r.MediaId).HasColumnName("media_id").IsRequired();
        builder.Property(r => r.CreatedAt).HasColumnName("created_at").IsRequired();

        builder.HasIndex(r => new { r.PageId, r.MediaId }).HasDatabaseName("uq_nte010_page_media").IsUnique();
        builder.HasIndex(r => r.MediaId).HasDatabaseName("ix_nte010_media_id");

        // Declared so a page created with media in its body is inserted before its rows.
        builder.HasOne<Page>().WithMany().HasForeignKey(r => r.PageId).HasConstraintName("fk_nte010_page");
        builder.HasOne<MediaItem>().WithMany().HasForeignKey(r => r.MediaId).HasConstraintName("fk_nte010_media");
    }
}

internal sealed class MediaRenditionEntityConfiguration : IEntityTypeConfiguration<MediaRendition>
{
    public void Configure(EntityTypeBuilder<MediaRendition> builder)
    {
        builder.ToTable("nte009_media_rendition", NotesModule.Schema);
        builder.HasKey(r => r.Id).HasName("pk_nte009");

        builder.Property(r => r.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(r => r.MediaId).HasColumnName("media_id").IsRequired();
        builder.Property(r => r.Purpose).HasColumnName("purpose").HasMaxLength(20).IsRequired();
        builder.Property(r => r.Width).HasColumnName("width").IsRequired();
        builder.Property(r => r.Height).HasColumnName("height").IsRequired();
        builder.Property(r => r.ContentType).HasColumnName("content_type").HasMaxLength(255).IsRequired();
        builder.Property(r => r.SizeBytes).HasColumnName("size_bytes").IsRequired();
        builder.Property(r => r.StorageBackend).HasColumnName("storage_backend").HasMaxLength(50).IsRequired();
        builder.Property(r => r.StorageKey).HasColumnName("storage_key").HasMaxLength(1024).IsRequired();
        builder.Property(r => r.CreatedAt).HasColumnName("created_at").IsRequired();

        builder.HasIndex(r => new { r.MediaId, r.Purpose }).HasDatabaseName("uq_nte009_media_purpose").IsUnique();

        // Declared so EF inserts the item before its thumbnail when both are written together.
        builder.HasOne<MediaItem>().WithMany().HasForeignKey(r => r.MediaId).HasConstraintName("fk_nte009_media");
    }
}
