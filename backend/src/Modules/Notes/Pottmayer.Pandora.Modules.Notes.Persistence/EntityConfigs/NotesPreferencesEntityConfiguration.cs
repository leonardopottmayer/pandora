using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pottmayer.Pandora.Modules.Notes.Abstractions;
using Pottmayer.Pandora.Modules.Notes.Domain.Aggregates;

namespace Pottmayer.Pandora.Modules.Notes.Persistence.EntityConfigs;

internal sealed class NotesPreferencesEntityConfiguration : IEntityTypeConfiguration<NotesPreferences>
{
    public void Configure(EntityTypeBuilder<NotesPreferences> builder)
    {
        builder.ToTable("nte016_notes_preferences", NotesModule.Schema);
        builder.HasKey(p => p.UserId).HasName("pk_nte016");
        builder.Property(p => p.UserId).HasColumnName("user_id").ValueGeneratedNever();
        builder.Property(p => p.DailyTemplateId).HasColumnName("daily_template_id");
        builder.Property(p => p.DailyFolderId).HasColumnName("daily_folder_id");
        builder.Property(p => p.CreatedAt).HasColumnName("created_at");
        builder.Property(p => p.CreatedBy).HasColumnName("created_by");
        builder.Property(p => p.UpdatedAt).HasColumnName("updated_at");
        builder.Property(p => p.UpdatedBy).HasColumnName("updated_by");
        // Optional references are owner-checked on use and may outlive trash/purge.
    }
}
