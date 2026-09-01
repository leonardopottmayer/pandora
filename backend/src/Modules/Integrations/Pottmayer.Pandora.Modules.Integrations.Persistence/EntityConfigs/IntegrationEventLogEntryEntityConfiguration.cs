using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pottmayer.Pandora.Modules.Integrations.Abstractions;
using Pottmayer.Pandora.Modules.Integrations.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Integrations.Domain.ValueObjects;

namespace Pottmayer.Pandora.Modules.Integrations.Persistence.EntityConfigs;

internal sealed class IntegrationEventLogEntryEntityConfiguration
    : IEntityTypeConfiguration<IntegrationEventLogEntry>
{
    public void Configure(EntityTypeBuilder<IntegrationEventLogEntry> builder)
    {
        builder.ToTable("int003_integration_event_log", IntegrationsModule.Schema);

        builder.HasKey(e => e.Id).HasName("pk_int003");

        builder.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(e => e.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(e => e.ExternalAccountId).HasColumnName("external_account_id");

        builder.Property(e => e.Provider)
               .HasColumnName("provider")
               .HasMaxLength(40)
               .IsRequired();

        builder.Property(e => e.EventType)
               .HasColumnName("event_type")
               .HasConversion(t => t.Value, v => IntegrationEventType.FromValue(v))
               .HasMaxLength(30)
               .IsRequired();

        builder.Property(e => e.Detail).HasColumnName("detail").HasColumnType("text");
        builder.Property(e => e.OccurredAt).HasColumnName("occurred_at").IsRequired();

        // The read path: a user's timeline, newest first.
        builder.HasIndex(e => new { e.UserId, e.OccurredAt })
               .HasDatabaseName("ix_int003_user_occurred");
    }
}
