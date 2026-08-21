using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pottmayer.Pandora.Modules.Agenda.Abstractions;
using Pottmayer.Pandora.Modules.Agenda.Domain.Aggregates;

namespace Pottmayer.Pandora.Modules.Agenda.Persistence.EntityConfigs;

internal sealed class EventEntityConfiguration : IEntityTypeConfiguration<Event>
{
    public void Configure(EntityTypeBuilder<Event> builder)
    {
        builder.ToTable("agd002_event", AgendaModule.Schema);

        builder.HasKey(e => e.Id).HasName("pk_agd002");
        builder.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(e => e.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(e => e.CalendarId).HasColumnName("calendar_id").IsRequired();
        builder.Property(e => e.Title).HasColumnName("title").HasMaxLength(200).IsRequired();
        builder.Property(e => e.Description).HasColumnName("description");
        builder.Property(e => e.Location).HasColumnName("location");
        builder.Property(e => e.Url).HasColumnName("url");
        builder.Property(e => e.StartsAt).HasColumnName("starts_at").IsRequired();
        builder.Property(e => e.EndsAt).HasColumnName("ends_at").IsRequired();
        builder.Property(e => e.IsAllDay).HasColumnName("is_all_day").IsRequired();
        builder.Property(e => e.TimeZone).HasColumnName("time_zone").HasMaxLength(100).IsRequired();
        builder.Property(e => e.Rrule).HasColumnName("rrule");
        builder.Property(e => e.RecurrenceEndsAt).HasColumnName("recurrence_ends_at");
        builder.Property(e => e.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(e => e.DeletedAt).HasColumnName("deleted_at");

        builder.HasIndex(e => e.UserId).HasDatabaseName("ix_agd002_user_id");
        builder.HasIndex(e => e.CalendarId).HasDatabaseName("ix_agd002_calendar_id");

        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(e => e.CreatedBy).HasColumnName("created_by");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        builder.Property(e => e.UpdatedBy).HasColumnName("updated_by");
    }
}
