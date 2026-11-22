using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pottmayer.Pandora.Modules.Agenda.Abstractions;
using Pottmayer.Pandora.Modules.Agenda.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Agenda.Domain.ValueObjects;

namespace Pottmayer.Pandora.Modules.Agenda.Persistence.EntityConfigs;

internal sealed class TaskListBindingEntityConfiguration : IEntityTypeConfiguration<TaskListBinding>
{
    public void Configure(EntityTypeBuilder<TaskListBinding> builder)
    {
        builder.ToTable("agd012_task_list_binding", AgendaModule.Schema);
        builder.HasKey(b => b.Id).HasName("pk_agd012");

        builder.Property(b => b.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(b => b.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(b => b.ListId).HasColumnName("list_id").IsRequired();
        builder.Property(b => b.ExternalAccountId).HasColumnName("external_account_id").IsRequired();
        builder.Property(b => b.RemoteListId).HasColumnName("remote_list_id").HasMaxLength(255).IsRequired();
        builder.Property(b => b.UpdatedCursor).HasColumnName("updated_cursor");
        builder.Property(b => b.FullSyncedAt).HasColumnName("full_synced_at");
        builder.Property(b => b.LastSyncedAt).HasColumnName("last_synced_at");
        builder.Property(b => b.Status).HasColumnName("status").HasMaxLength(20).IsRequired()
               .HasConversion(s => s.Value, v => CalendarBindingStatus.FromValue(v));
        builder.Property(b => b.FailureCount).HasColumnName("failure_count").IsRequired();
        builder.Property(b => b.LastError).HasColumnName("last_error").HasMaxLength(1000);

        builder.HasOne<TaskList>().WithMany().HasForeignKey(b => b.ListId)
               .HasConstraintName("fk_agd012_list").OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(b => b.ListId).IsUnique().HasDatabaseName("uq_agd012_list");
        builder.HasIndex(b => new { b.ExternalAccountId, b.RemoteListId }).IsUnique().HasDatabaseName("uq_agd012_account_remote");
        builder.HasIndex(b => b.UserId).HasDatabaseName("ix_agd012_user_id");

        builder.Property(b => b.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(b => b.CreatedBy).HasColumnName("created_by");
        builder.Property(b => b.UpdatedAt).HasColumnName("updated_at");
        builder.Property(b => b.UpdatedBy).HasColumnName("updated_by");
    }
}
