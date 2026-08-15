using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pottmayer.Pandora.Modules.Notes.Abstractions;
using Pottmayer.Pandora.Modules.Notes.Domain.Aggregates;

namespace Pottmayer.Pandora.Modules.Notes.Persistence.EntityConfigs;

internal sealed class PageTagEntityConfiguration : IEntityTypeConfiguration<PageTag>
{
    public void Configure(EntityTypeBuilder<PageTag> builder)
    {
        builder.ToTable("nte006_page_tag", NotesModule.Schema);

        builder.HasKey(t => t.Id).HasName("pk_nte006");

        builder.Property(t => t.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(t => t.PageId).HasColumnName("page_id").IsRequired();
        builder.Property(t => t.TagId).HasColumnName("tag_id").IsRequired();
        builder.Property(t => t.CreatedAt).HasColumnName("created_at").IsRequired();

        // No navigation, but the relationship is declared so EF knows the order to save in: the
        // sweep that deletes an orphaned tag removes its page_tag rows in the same transaction, and
        // without this the tag would go first and hit fk_nte006_tag.
        builder.HasOne<Tag>().WithMany().HasForeignKey(t => t.TagId).OnDelete(DeleteBehavior.Cascade);

        // A tag written five times in one page is one fact.
        builder.HasIndex(t => new { t.PageId, t.TagId })
               .HasDatabaseName("uq_nte006_page_tag")
               .IsUnique();

        // "Which pages carry this tag?" is the filter's read.
        builder.HasIndex(t => t.TagId).HasDatabaseName("ix_nte006_tag_id");
    }
}
