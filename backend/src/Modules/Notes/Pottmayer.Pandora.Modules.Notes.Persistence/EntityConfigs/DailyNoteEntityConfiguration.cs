using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pottmayer.Pandora.Modules.Notes.Abstractions;
using Pottmayer.Pandora.Modules.Notes.Domain.Aggregates;

namespace Pottmayer.Pandora.Modules.Notes.Persistence.EntityConfigs;

internal sealed class DailyNoteEntityConfiguration : IEntityTypeConfiguration<DailyNote>
{
    public void Configure(EntityTypeBuilder<DailyNote> builder)
    {
        builder.ToTable("nte017_daily_note", NotesModule.Schema);
        builder.HasKey(p => p.Id).HasName("pk_nte017");
        builder.Property(p => p.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(p => p.UserId).HasColumnName("user_id");
        builder.Property(p => p.Date).HasColumnName("date");
        builder.Property(p => p.PageId).HasColumnName("page_id");
        builder.HasIndex(p => new { p.UserId, p.Date }).IsUnique().HasDatabaseName("uq_nte017_user_date");
        // No page FK: a purged page is replaced on the next visit, without losing the day's identity.
    }
}
