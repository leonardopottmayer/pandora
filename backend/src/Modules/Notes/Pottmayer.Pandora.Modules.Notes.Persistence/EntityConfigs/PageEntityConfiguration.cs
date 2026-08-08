using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pottmayer.Pandora.Modules.Notes.Abstractions;
using Pottmayer.Pandora.Modules.Notes.Domain.Aggregates;

namespace Pottmayer.Pandora.Modules.Notes.Persistence.EntityConfigs;

internal sealed class PageEntityConfiguration : IEntityTypeConfiguration<Page>
{
    public void Configure(EntityTypeBuilder<Page> builder)
    {
        builder.ToTable("nte001_page", NotesModule.Schema);

        builder.HasKey(p => p.Id).HasName("pk_nte001");

        builder.Property(p => p.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(p => p.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(p => p.ParentId).HasColumnName("parent_id");
        builder.Property(p => p.Title).HasColumnName("title").HasMaxLength(255).IsRequired();
        builder.Property(p => p.Slug).HasColumnName("slug").HasMaxLength(100).IsRequired();
        builder.Property(p => p.ContentMarkdown).HasColumnName("content_markdown").IsRequired();
        builder.Property(p => p.Icon).HasColumnName("icon").HasMaxLength(50);
        builder.Property(p => p.OrderIndex).HasColumnName("order_index").IsRequired();
        builder.Property(p => p.IsFavorite).HasColumnName("is_favorite").IsRequired();
        builder.Property(p => p.ArchivedAt).HasColumnName("archived_at");
        builder.Property(p => p.DeletedAt).HasColumnName("deleted_at");

        builder.Property(p => p.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(p => p.CreatedBy).HasColumnName("created_by");
        builder.Property(p => p.UpdatedAt).HasColumnName("updated_at");
        builder.Property(p => p.UpdatedBy).HasColumnName("updated_by");

        builder.HasIndex(p => p.UserId).HasDatabaseName("ix_nte001_user_id");
        builder.HasIndex(p => p.ParentId).HasDatabaseName("ix_nte001_parent_id");

        // Slug is unique per user among live pages; the partial filter lets a slug be reused after
        // its page is soft-deleted, matching ExistsWithSlugAsync (which ignores deleted rows).
        builder.HasIndex(p => new { p.UserId, p.Slug })
               .HasDatabaseName("uq_nte001_user_slug")
               .IsUnique()
               .HasFilter("deleted_at IS NULL");
    }
}
