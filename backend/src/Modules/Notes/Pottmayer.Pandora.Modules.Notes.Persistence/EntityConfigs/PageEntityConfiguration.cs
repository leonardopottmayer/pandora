using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NpgsqlTypes;
using Pottmayer.Pandora.Modules.Notes.Abstractions;
using Pottmayer.Pandora.Modules.Notes.Domain.Aggregates;

namespace Pottmayer.Pandora.Modules.Notes.Persistence.EntityConfigs;

/// <summary>Names of the page columns that exist only in the store, shared by the config and the query.</summary>
internal static class PageColumns
{
    public const string SearchVector = "SearchVector";
}

internal sealed class PageEntityConfiguration : IEntityTypeConfiguration<Page>
{
    public void Configure(EntityTypeBuilder<Page> builder)
    {
        builder.ToTable("nte001_page", NotesModule.Schema);

        builder.HasKey(p => p.Id).HasName("pk_nte001");

        builder.Property(p => p.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(p => p.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(p => p.SpaceId).HasColumnName("space_id").IsRequired();
        builder.Property(p => p.ParentId).HasColumnName("parent_id");
        builder.Property(p => p.FolderId).HasColumnName("folder_id");
        builder.Property(p => p.Title).HasColumnName("title").HasMaxLength(255).IsRequired();
        builder.Property(p => p.Slug).HasColumnName("slug").HasMaxLength(100).IsRequired();
        builder.Property(p => p.Content).HasColumnName("content").HasColumnType("jsonb").IsRequired();
        builder.Property(p => p.ContentText).HasColumnName("content_text").IsRequired();
        builder.Property(p => p.Icon).HasColumnName("icon").HasMaxLength(50);
        builder.Property(p => p.OrderIndex).HasColumnName("order_index").IsRequired();
        builder.Property(p => p.IsFavorite).HasColumnName("is_favorite").IsRequired();
        builder.Property(p => p.IsTemplate).HasColumnName("is_template").IsRequired();
        builder.Property(p => p.ArchivedAt).HasColumnName("archived_at");
        builder.Property(p => p.DeletedAt).HasColumnName("deleted_at");
        builder.Property(p => p.Appearance).HasColumnName("appearance").HasColumnType("jsonb").IsRequired();
        builder.Property(p => p.IsLocked).HasColumnName("is_locked").IsRequired();
        builder.Property(p => p.VerifiedAt).HasColumnName("verified_at");
        builder.Property(p => p.VerifiedUntil).HasColumnName("verified_until");
        builder.Property(p => p.ViewedAt).HasColumnName("viewed_at");
        builder.Property(p => p.PublicToken).HasColumnName("public_token").HasMaxLength(64);
        builder.Property(p => p.HistoryNumber).HasColumnName("history_number").IsConcurrencyToken();
        builder.Ignore(p => p.HistorySource);
        builder.Ignore(p => p.RestoredFrom);

        builder.Property(p => p.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(p => p.CreatedBy).HasColumnName("created_by");
        builder.Property(p => p.UpdatedAt).HasColumnName("updated_at");
        builder.Property(p => p.UpdatedBy).HasColumnName("updated_by");

        // Full-text vector over title + body. It is a shadow property because the search vector is a
        // storage detail (Postgres generates it), not something the aggregate knows about; the search
        // query reaches it with EF.Property.
        builder.Property<NpgsqlTsVector>(PageColumns.SearchVector)
               .HasColumnName("search_vector")
               .HasComputedColumnSql(
                   "to_tsvector('simple', coalesce(title, '') || ' ' || coalesce(content_text, ''))",
                   stored: true);

        builder.HasIndex(p => p.UserId).HasDatabaseName("ix_nte001_user_id");
        builder.HasIndex(p => p.ParentId).HasDatabaseName("ix_nte001_parent_id");
        builder.HasIndex(p => p.SpaceId).HasDatabaseName("ix_nte001_space_id");
        builder.HasIndex(p => p.FolderId).HasDatabaseName("ix_nte001_folder_id");
        builder.HasIndex(p => p.PublicToken).HasDatabaseName("uq_nte001_public_token").IsUnique().HasFilter("public_token IS NOT NULL");
        builder.HasOne<Folder>().WithMany().HasForeignKey(p => p.FolderId).HasConstraintName("fk_nte001_folder");

        // Declared so EF inserts a space created on the fly before the page that goes in it.
        builder.HasOne<Space>().WithMany().HasForeignKey(p => p.SpaceId).HasConstraintName("fk_nte001_space");

        // Slug is unique per user among live pages; the partial filter lets a slug be reused after
        // its page is soft-deleted, matching ExistsWithSlugAsync (which ignores deleted rows).
        builder.HasIndex(p => new { p.UserId, p.Slug })
               .HasDatabaseName("uq_nte001_user_slug")
               .IsUnique()
               .HasFilter("deleted_at IS NULL");
    }
}
