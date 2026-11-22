using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pottmayer.Pandora.Modules.Agenda.Abstractions;
using Pottmayer.Pandora.Modules.Agenda.Domain.Aggregates;

namespace Pottmayer.Pandora.Modules.Agenda.Persistence.EntityConfigs;

internal sealed class TaskLinkEntityConfiguration : IEntityTypeConfiguration<TaskLink>
{
    public void Configure(EntityTypeBuilder<TaskLink> builder)
    {
        builder.ToTable("agd014_task_link", AgendaModule.Schema);
        builder.HasKey(l => l.Id).HasName("pk_agd014");

        builder.Property(l => l.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(l => l.BindingId).HasColumnName("binding_id").IsRequired();
        builder.Property(l => l.TaskId).HasColumnName("task_id").IsRequired();
        builder.Property(l => l.RemoteTaskId).HasColumnName("remote_task_id").HasMaxLength(1024);
        builder.Property(l => l.Etag).HasColumnName("etag").HasMaxLength(255);
        builder.Property(l => l.RemoteUpdatedAt).HasColumnName("remote_updated_at");
        builder.Property(l => l.PendingPushAt).HasColumnName("pending_push_at");
        builder.Property(l => l.CreatedAt).HasColumnName("created_at").IsRequired();

        // Declared so EF orders a task's insert before its link's, and a link's delete before its binding's.
        builder.HasOne<TaskListBinding>().WithMany().HasForeignKey(l => l.BindingId)
               .HasConstraintName("fk_agd014_binding").OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<TaskItem>().WithMany().HasForeignKey(l => l.TaskId)
               .HasConstraintName("fk_agd014_task").OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(l => new { l.BindingId, l.RemoteTaskId }).IsUnique().HasDatabaseName("uq_agd014_binding_remote");
        builder.HasIndex(l => l.TaskId).IsUnique().HasDatabaseName("uq_agd014_task");
    }
}
