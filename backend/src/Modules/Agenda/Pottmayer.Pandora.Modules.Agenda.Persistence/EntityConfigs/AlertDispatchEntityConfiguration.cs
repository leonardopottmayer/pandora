using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pottmayer.Pandora.Modules.Agenda.Abstractions;
using Pottmayer.Pandora.Modules.Agenda.Domain.Aggregates;

namespace Pottmayer.Pandora.Modules.Agenda.Persistence.EntityConfigs;

internal sealed class AlertDispatchEntityConfiguration : IEntityTypeConfiguration<AlertDispatch>
{
    public void Configure(EntityTypeBuilder<AlertDispatch> builder)
    {
        builder.ToTable("agd008_alert_dispatch", AgendaModule.Schema);

        builder.HasKey(d => d.Id).HasName("pk_agd008");
        builder.Property(d => d.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(d => d.AlertId).HasColumnName("alert_id").IsRequired();
        builder.Property(d => d.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(d => d.OccurrenceStartsAt).HasColumnName("occurrence_starts_at").IsRequired();
        builder.Property(d => d.DispatchedAt).HasColumnName("dispatched_at").IsRequired();
        builder.Property(d => d.CorrelationId).HasColumnName("correlation_id").IsRequired();
        builder.Property(d => d.IsLate).HasColumnName("is_late").IsRequired();

        // Declares the FK so EF orders the alert insert before its dispatch rows; matches ON DELETE CASCADE.
        builder.HasOne<Alert>()
               .WithMany()
               .HasForeignKey(d => d.AlertId)
               .HasConstraintName("fk_agd008_alert")
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(d => new { d.AlertId, d.OccurrenceStartsAt })
               .IsUnique()
               .HasDatabaseName("uq_agd008_alert_occurrence");

        builder.HasIndex(d => d.AlertId).HasDatabaseName("ix_agd008_alert_id");

        builder.Property(d => d.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(d => d.CreatedBy).HasColumnName("created_by");
        builder.Property(d => d.UpdatedAt).HasColumnName("updated_at");
        builder.Property(d => d.UpdatedBy).HasColumnName("updated_by");
    }
}
