using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pottmayer.Pandora.Modules.Agenda.Abstractions;
using Pottmayer.Pandora.Modules.Agenda.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Agenda.Domain.ValueObjects;

namespace Pottmayer.Pandora.Modules.Agenda.Persistence.EntityConfigs;

internal sealed class CalendarBindingEntityConfiguration : IEntityTypeConfiguration<CalendarBinding>
{
    public void Configure(EntityTypeBuilder<CalendarBinding> builder)
    {
        builder.ToTable("agd009_calendar_binding", AgendaModule.Schema);
        builder.HasKey(b => b.Id).HasName("pk_agd009");

        builder.Property(b => b.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(b => b.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(b => b.CalendarId).HasColumnName("calendar_id").IsRequired();
        builder.Property(b => b.ExternalAccountId).HasColumnName("external_account_id").IsRequired();
        builder.Property(b => b.RemoteCalendarId).HasColumnName("remote_calendar_id").HasMaxLength(255).IsRequired();
        builder.Property(b => b.Direction).HasColumnName("direction").HasMaxLength(20).IsRequired()
               .HasConversion(d => d.Value, v => CalendarBindingDirection.FromValue(v));
        builder.Property(b => b.SyncToken).HasColumnName("sync_token");
        builder.Property(b => b.FullSyncedAt).HasColumnName("full_synced_at");
        builder.Property(b => b.LastSyncedAt).HasColumnName("last_synced_at");
        builder.Property(b => b.Status).HasColumnName("status").HasMaxLength(20).IsRequired()
               .HasConversion(s => s.Value, v => CalendarBindingStatus.FromValue(v));
        builder.Property(b => b.FailureCount).HasColumnName("failure_count").IsRequired();
        builder.Property(b => b.LastError).HasColumnName("last_error").HasMaxLength(1000);

        builder.HasOne<Calendar>().WithMany().HasForeignKey(b => b.CalendarId)
               .HasConstraintName("fk_agd009_calendar").OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(b => b.CalendarId).IsUnique().HasDatabaseName("uq_agd009_calendar");
        builder.HasIndex(b => new { b.ExternalAccountId, b.RemoteCalendarId }).IsUnique().HasDatabaseName("uq_agd009_account_remote");
        builder.HasIndex(b => b.UserId).HasDatabaseName("ix_agd009_user_id");

        builder.Property(b => b.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(b => b.CreatedBy).HasColumnName("created_by");
        builder.Property(b => b.UpdatedAt).HasColumnName("updated_at");
        builder.Property(b => b.UpdatedBy).HasColumnName("updated_by");
    }
}
