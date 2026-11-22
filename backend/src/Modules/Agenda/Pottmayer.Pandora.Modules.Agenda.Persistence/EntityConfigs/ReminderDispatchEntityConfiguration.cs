using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pottmayer.Pandora.Modules.Agenda.Abstractions;
using Pottmayer.Pandora.Modules.Agenda.Domain.Aggregates;

namespace Pottmayer.Pandora.Modules.Agenda.Persistence.EntityConfigs;

internal sealed class ReminderDispatchEntityConfiguration : IEntityTypeConfiguration<ReminderDispatch>
{
    public void Configure(EntityTypeBuilder<ReminderDispatch> builder)
    {
        builder.ToTable("agd006x_reminder_dispatch", AgendaModule.Schema);

        builder.HasKey(d => d.Id)
               .HasName("pk_agd006x");

        builder.Property(d => d.Id)
               .HasColumnName("id")
               .ValueGeneratedNever();

        builder.Property(d => d.ReminderId)
               .HasColumnName("reminder_id")
               .IsRequired();

        builder.Property(d => d.UserId)
               .HasColumnName("user_id")
               .IsRequired();

        builder.Property(d => d.OccurrenceStartsAt)
               .HasColumnName("occurrence_starts_at")
               .IsRequired();

        builder.Property(d => d.DispatchedAt)
               .HasColumnName("dispatched_at")
               .IsRequired();

        builder.Property(d => d.CorrelationId)
               .HasColumnName("correlation_id")
               .IsRequired();

        builder.Property(d => d.IsLate)
               .HasColumnName("is_late")
               .IsRequired();

        builder.Property(d => d.AcknowledgedAt)
               .HasColumnName("acknowledged_at");

        builder.Property(d => d.SnoozedUntil)
               .HasColumnName("snoozed_until");

        // Declares the FK so EF orders the reminder insert before its dispatch rows; matches the
        // migration's ON DELETE CASCADE.
        builder.HasOne<Reminder>()
               .WithMany()
               .HasForeignKey(d => d.ReminderId)
               .HasConstraintName("fk_agd006x_reminder")
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(d => new { d.ReminderId, d.OccurrenceStartsAt })
               .IsUnique()
               .HasDatabaseName("uq_agd006x_reminder_occurrence");

        builder.HasIndex(d => d.ReminderId)
               .HasDatabaseName("ix_agd006x_reminder_id");

        builder.Property(d => d.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(d => d.CreatedBy).HasColumnName("created_by");
        builder.Property(d => d.UpdatedAt).HasColumnName("updated_at");
        builder.Property(d => d.UpdatedBy).HasColumnName("updated_by");
    }
}
