using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pottmayer.Pandora.Modules.Agenda.Abstractions;
using Pottmayer.Pandora.Modules.Agenda.Domain.Aggregates;

namespace Pottmayer.Pandora.Modules.Agenda.Persistence.EntityConfigs;

internal sealed class EventLinkEntityConfiguration : IEntityTypeConfiguration<EventLink>
{
    public void Configure(EntityTypeBuilder<EventLink> builder)
    {
        builder.ToTable("agd010_event_link", AgendaModule.Schema);
        builder.HasKey(l => l.Id).HasName("pk_agd010");

        builder.Property(l => l.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(l => l.BindingId).HasColumnName("binding_id").IsRequired();
        builder.Property(l => l.EventId).HasColumnName("event_id").IsRequired();
        builder.Property(l => l.RemoteEventId).HasColumnName("remote_event_id").HasMaxLength(1024).IsRequired();
        builder.Property(l => l.SeriesRemoteId).HasColumnName("series_remote_id").HasMaxLength(1024);
        builder.Property(l => l.Etag).HasColumnName("etag").HasMaxLength(255);
        builder.Property(l => l.RemoteUpdatedAt).HasColumnName("remote_updated_at");
        builder.Property(l => l.RemindersHash).HasColumnName("reminders_hash").HasMaxLength(64);
        builder.Property(l => l.CreatedAt).HasColumnName("created_at").IsRequired();

        // Declared so EF orders an event's insert before its link's, and a link's delete before its binding's.
        builder.HasOne<CalendarBinding>().WithMany().HasForeignKey(l => l.BindingId)
               .HasConstraintName("fk_agd010_binding").OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Event>().WithMany().HasForeignKey(l => l.EventId)
               .HasConstraintName("fk_agd010_event").OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(l => new { l.BindingId, l.RemoteEventId }).IsUnique().HasDatabaseName("uq_agd010_binding_remote");
        builder.HasIndex(l => l.EventId).IsUnique().HasDatabaseName("uq_agd010_event");
    }
}
