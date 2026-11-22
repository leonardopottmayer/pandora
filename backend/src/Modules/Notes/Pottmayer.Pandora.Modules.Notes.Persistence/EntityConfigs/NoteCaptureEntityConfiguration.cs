using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pottmayer.Pandora.Modules.Notes.Abstractions;
using Pottmayer.Pandora.Modules.Notes.Domain.Aggregates;

namespace Pottmayer.Pandora.Modules.Notes.Persistence.EntityConfigs;

internal sealed class NoteCaptureEntityConfiguration : IEntityTypeConfiguration<NoteCapture>
{
    public void Configure(EntityTypeBuilder<NoteCapture> builder)
    {
        builder.ToTable("nte015_note_capture", NotesModule.Schema);
        builder.HasKey(c => c.Id).HasName("pk_nte015");
        builder.Property(c => c.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(c => c.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(c => c.PageId).HasColumnName("page_id").IsRequired();
        builder.Property(c => c.CapturedAt).HasColumnName("captured_at").IsRequired();
        builder.HasIndex(c => new { c.UserId, c.PageId }).HasDatabaseName("ix_nte015_user_page");
        // No page FK: deleting a page must not make a retried event capture the same text again.
    }
}
