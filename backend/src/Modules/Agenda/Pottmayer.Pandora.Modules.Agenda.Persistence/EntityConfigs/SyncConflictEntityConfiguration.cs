using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pottmayer.Pandora.Modules.Agenda.Abstractions;
using Pottmayer.Pandora.Modules.Agenda.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Agenda.Domain.ValueObjects;

namespace Pottmayer.Pandora.Modules.Agenda.Persistence.EntityConfigs;

internal sealed class SyncConflictEntityConfiguration : IEntityTypeConfiguration<SyncConflict>
{
    public void Configure(EntityTypeBuilder<SyncConflict> builder)
    {
        builder.ToTable("agd011_sync_conflict", AgendaModule.Schema);
        builder.HasKey(c => c.Id).HasName("pk_agd011");

        builder.Property(c => c.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(c => c.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(c => c.BindingId).HasColumnName("binding_id").IsRequired();
        builder.Property(c => c.EventId).HasColumnName("event_id").IsRequired();
        builder.Property(c => c.Title).HasColumnName("title").HasMaxLength(200).IsRequired();
        builder.Property(c => c.LosingSide).HasColumnName("losing_side").HasMaxLength(20).IsRequired()
               .HasConversion(s => s.Value, v => SyncConflictSide.FromValue(v));
        builder.Property(c => c.LosingVersion).HasColumnName("losing_version").HasColumnType("jsonb").IsRequired();
        builder.Property(c => c.DetectedAt).HasColumnName("detected_at").IsRequired();

        builder.HasIndex(c => new { c.UserId, c.DetectedAt }).HasDatabaseName("ix_agd011_user_detected");
    }
}
