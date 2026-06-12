using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pottmayer.Pandora.Modules.Finances.Abstractions;
using Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;

namespace Pottmayer.Pandora.Modules.Finances.Persistence.EntityConfigs;

internal sealed class AuditEventEntityConfiguration : IEntityTypeConfiguration<AuditEvent>
{
    public void Configure(EntityTypeBuilder<AuditEvent> builder)
    {
        builder.ToTable("fin016_audit_event", FinancesModule.Schema);

        builder.HasKey(e => e.Id)
               .HasName("pk_fin016");

        builder.Property(e => e.Id)
               .HasColumnName("id")
               .ValueGeneratedNever();

        builder.Property(e => e.UserId)
               .HasColumnName("user_id")
               .IsRequired();

        builder.Property(e => e.ActorUserId)
               .HasColumnName("actor_user_id");

        builder.Property(e => e.EntityType)
               .HasColumnName("entity_type")
               .HasMaxLength(40)
               .IsRequired();

        builder.Property(e => e.EntityId)
               .HasColumnName("entity_id")
               .IsRequired();

        builder.Property(e => e.EventType)
               .HasColumnName("event_type")
               .HasMaxLength(60)
               .IsRequired();

        builder.Property(e => e.Data)
               .HasColumnName("data")
               .HasColumnType("jsonb");

        builder.Property(e => e.CorrelationId)
               .HasColumnName("correlation_id");

        builder.Property(e => e.OccurredAt)
               .HasColumnName("occurred_at")
               .IsRequired();

        builder.HasIndex(e => new { e.EntityType, e.EntityId, e.OccurredAt })
               .HasDatabaseName("ix_fin016_entity");

        builder.HasIndex(e => new { e.UserId, e.OccurredAt })
               .HasDatabaseName("ix_fin016_user_occurred_at");
    }
}
