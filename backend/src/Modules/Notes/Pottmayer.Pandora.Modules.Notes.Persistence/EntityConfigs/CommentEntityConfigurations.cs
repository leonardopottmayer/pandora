using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pottmayer.Pandora.Modules.Notes.Abstractions;
using Pottmayer.Pandora.Modules.Notes.Domain.Aggregates;

namespace Pottmayer.Pandora.Modules.Notes.Persistence.EntityConfigs;

internal sealed class PageCommentEntityConfiguration : IEntityTypeConfiguration<PageComment>
{
    public void Configure(EntityTypeBuilder<PageComment> builder)
    {
        builder.ToTable("nte013_page_comment", NotesModule.Schema);
        builder.HasKey(c => c.Id).HasName("pk_nte013");

        builder.Property(c => c.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(c => c.PageId).HasColumnName("page_id").IsRequired();
        builder.Property(c => c.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(c => c.BlockId).HasColumnName("block_id").HasMaxLength(PageComment.MaxBlockId).IsRequired();
        builder.Property(c => c.ThreadId).HasColumnName("thread_id");
        builder.Property(c => c.Quote).HasColumnName("quote").HasMaxLength(PageComment.MaxQuote);
        builder.Property(c => c.Body).HasColumnName("body").IsRequired();
        builder.Property(c => c.ResolvedAt).HasColumnName("resolved_at");
        builder.Property(c => c.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(c => c.EditedAt).HasColumnName("edited_at");
        builder.Ignore(c => c.IsThread);

        builder.HasIndex(c => c.PageId).HasDatabaseName("ix_nte013_page_id");
        builder.HasOne<Page>().WithMany().HasForeignKey(c => c.PageId).HasConstraintName("fk_nte013_page");
        builder.HasOne<PageComment>().WithMany().HasForeignKey(c => c.ThreadId).HasConstraintName("fk_nte013_thread");
    }
}

internal sealed class BlockStampEntityConfiguration : IEntityTypeConfiguration<BlockStamp>
{
    public void Configure(EntityTypeBuilder<BlockStamp> builder)
    {
        builder.ToTable("nte014_block_stamp", NotesModule.Schema);
        builder.HasKey(s => s.Id).HasName("pk_nte014");

        builder.Property(s => s.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(s => s.PageId).HasColumnName("page_id").IsRequired();
        builder.Property(s => s.BlockId).HasColumnName("block_id").HasMaxLength(64).IsRequired();
        builder.Property(s => s.Fingerprint).HasColumnName("fingerprint").HasMaxLength(32).IsRequired();
        builder.Property(s => s.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(s => s.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.Property(s => s.UpdatedSource).HasColumnName("updated_source").HasMaxLength(20).IsRequired();

        builder.HasIndex(s => new { s.PageId, s.BlockId }).IsUnique().HasDatabaseName("uq_nte014_page_block");
        // Declared so a new page is inserted before its stamps.
        builder.HasOne<Page>().WithMany().HasForeignKey(s => s.PageId).HasConstraintName("fk_nte014_page");
    }
}
