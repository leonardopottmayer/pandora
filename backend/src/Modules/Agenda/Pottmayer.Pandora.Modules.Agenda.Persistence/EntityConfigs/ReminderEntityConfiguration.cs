using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pottmayer.Pandora.Modules.Agenda.Abstractions;
using Pottmayer.Pandora.Modules.Agenda.Domain.Aggregates;

namespace Pottmayer.Pandora.Modules.Agenda.Persistence.EntityConfigs;

internal sealed class ReminderEntityConfiguration : IEntityTypeConfiguration<Reminder>
{
    public void Configure(EntityTypeBuilder<Reminder> builder)
    {
        builder.ToTable("agd006_reminder", AgendaModule.Schema);

        builder.HasKey(r => r.Id)
               .HasName("pk_agd006");

        builder.Property(r => r.Id)
               .HasColumnName("id")
               .ValueGeneratedNever();

        builder.Property(r => r.UserId)
               .HasColumnName("user_id")
               .IsRequired();

        builder.Property(r => r.Title)
               .HasColumnName("title")
               .HasMaxLength(200)
               .IsRequired();

        builder.Property(r => r.Notes)
               .HasColumnName("notes")
               .HasColumnType("text");

        builder.Property(r => r.RemindAt)
               .HasColumnName("remind_at")
               .IsRequired();

        builder.Property(r => r.TimeZone)
               .HasColumnName("time_zone")
               .HasMaxLength(100)
               .IsRequired();

        builder.Property(r => r.Rrule)
               .HasColumnName("rrule")
               .HasColumnType("text");

        builder.Property(r => r.RecurrenceEndsAt)
               .HasColumnName("recurrence_ends_at");

        builder.Property(r => r.Status)
               .HasColumnName("status")
               .HasConversion<string>()
               .HasMaxLength(20)
               .IsRequired();

        builder.Property(r => r.SnoozedUntil)
               .HasColumnName("snoozed_until");

        builder.Property(r => r.AcknowledgedAt)
               .HasColumnName("acknowledged_at");

        builder.HasIndex(r => r.UserId)
               .HasDatabaseName("ix_agd006_user_id");

        builder.HasIndex(r => new { r.Status, r.RemindAt })
               .HasDatabaseName("ix_agd006_status_remind_at");

        builder.Property(r => r.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(r => r.CreatedBy).HasColumnName("created_by");
        builder.Property(r => r.UpdatedAt).HasColumnName("updated_at");
        builder.Property(r => r.UpdatedBy).HasColumnName("updated_by");
    }
}
