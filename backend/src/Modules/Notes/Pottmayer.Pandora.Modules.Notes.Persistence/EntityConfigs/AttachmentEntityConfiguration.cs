using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pottmayer.Pandora.Modules.Notes.Abstractions;
using Pottmayer.Pandora.Modules.Notes.Domain.Aggregates;

namespace Pottmayer.Pandora.Modules.Notes.Persistence.EntityConfigs;

internal sealed class AttachmentEntityConfiguration : IEntityTypeConfiguration<Attachment>
{
    public void Configure(EntityTypeBuilder<Attachment> builder)
    {
        builder.ToTable("nte002_attachment", NotesModule.Schema);

        builder.HasKey(a => a.Id).HasName("pk_nte002");

        builder.Property(a => a.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(a => a.PageId).HasColumnName("page_id");
        builder.Property(a => a.FileName).HasColumnName("file_name").HasMaxLength(255).IsRequired();
        builder.Property(a => a.ContentType).HasColumnName("content_type").HasMaxLength(255).IsRequired();
        builder.Property(a => a.SizeBytes).HasColumnName("size_bytes").IsRequired();
        builder.Property(a => a.StorageBackend).HasColumnName("storage_backend").HasMaxLength(50).IsRequired();
        builder.Property(a => a.StorageKey).HasColumnName("storage_key").HasMaxLength(1024).IsRequired();
        builder.Property(a => a.CreatedAt).HasColumnName("created_at").IsRequired();

        // A page may be soft-deleted while its attachment lingers, so no FK constraint — PageId is a
        // loose reference used only to group an upload under the page it was pasted into.
        builder.HasIndex(a => a.PageId).HasDatabaseName("ix_nte002_page_id");
    }
}
