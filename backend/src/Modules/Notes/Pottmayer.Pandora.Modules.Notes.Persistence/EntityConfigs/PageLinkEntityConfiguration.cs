using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pottmayer.Pandora.Modules.Notes.Abstractions;
using Pottmayer.Pandora.Modules.Notes.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Notes.Domain.ValueObjects;

namespace Pottmayer.Pandora.Modules.Notes.Persistence.EntityConfigs;

internal sealed class PageLinkEntityConfiguration : IEntityTypeConfiguration<PageLink>
{
    public void Configure(EntityTypeBuilder<PageLink> builder)
    {
        builder.ToTable("nte004_page_link", NotesModule.Schema);

        builder.HasKey(l => l.Id).HasName("pk_nte004");

        builder.Property(l => l.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(l => l.SourcePageId).HasColumnName("source_page_id").IsRequired();
        builder.Property(l => l.TargetPageId).HasColumnName("target_page_id").IsRequired();

        builder.Property(l => l.Kind)
               .HasColumnName("kind")
               .HasConversion(k => k.Value, v => PageLinkKind.FromValue(v))
               .HasMaxLength(20)
               .IsRequired();

        builder.Property(l => l.CreatedAt).HasColumnName("created_at").IsRequired();

        // One edge per (source, target, kind): a target linked twice in the same page is one fact.
        builder.HasIndex(l => new { l.SourcePageId, l.TargetPageId, l.Kind })
               .HasDatabaseName("uq_nte004_edge")
               .IsUnique();

        builder.HasIndex(l => l.TargetPageId).HasDatabaseName("ix_nte004_target_page_id");
    }
}
