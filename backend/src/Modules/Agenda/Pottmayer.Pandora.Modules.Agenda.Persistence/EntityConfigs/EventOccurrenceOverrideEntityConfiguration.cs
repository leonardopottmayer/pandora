using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pottmayer.Pandora.Modules.Agenda.Abstractions;
using Pottmayer.Pandora.Modules.Agenda.Domain.Aggregates;

namespace Pottmayer.Pandora.Modules.Agenda.Persistence.EntityConfigs;

internal sealed class EventOccurrenceOverrideEntityConfiguration : IEntityTypeConfiguration<EventOccurrenceOverride>
{
    public void Configure(EntityTypeBuilder<EventOccurrenceOverride> builder)
    {
        builder.ToTable("agd003_event_occurrence_override", AgendaModule.Schema);

        builder.HasKey(o => o.Id).HasName("pk_agd003");
        builder.Property(o => o.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(o => o.EventId).HasColumnName("event_id").IsRequired();
        builder.Property(o => o.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(o => o.OriginalStartsAt).HasColumnName("original_starts_at").IsRequired();
        builder.Property(o => o.IsCancelled).HasColumnName("is_cancelled").IsRequired();
        builder.Property(o => o.StartsAt).HasColumnName("starts_at");
        builder.Property(o => o.EndsAt).HasColumnName("ends_at");
        builder.Property(o => o.Title).HasColumnName("title").HasMaxLength(200);
        builder.Property(o => o.Description).HasColumnName("description");
        builder.Property(o => o.Location).HasColumnName("location");

        builder.HasIndex(o => new { o.EventId, o.OriginalStartsAt })
               .IsUnique()
               .HasDatabaseName("uq_agd003_event_occurrence");

        builder.Property(o => o.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(o => o.CreatedBy).HasColumnName("created_by");
        builder.Property(o => o.UpdatedAt).HasColumnName("updated_at");
        builder.Property(o => o.UpdatedBy).HasColumnName("updated_by");
    }
}
