using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pottmayer.Pandora.Modules.Agenda.Abstractions;
using Pottmayer.Pandora.Modules.Agenda.Domain.Aggregates;

namespace Pottmayer.Pandora.Modules.Agenda.Persistence.EntityConfigs;

internal sealed class TaskEntityConfiguration : IEntityTypeConfiguration<TaskItem>
{
    public void Configure(EntityTypeBuilder<TaskItem> builder)
    {
        builder.ToTable("agd005_task", AgendaModule.Schema);

        builder.HasKey(t => t.Id).HasName("pk_agd005");
        builder.Property(t => t.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(t => t.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(t => t.ListId).HasColumnName("list_id").IsRequired();
        builder.Property(t => t.ParentTaskId).HasColumnName("parent_task_id");
        builder.Property(t => t.Title).HasColumnName("title").HasMaxLength(200).IsRequired();
        builder.Property(t => t.Notes).HasColumnName("notes").HasColumnType("text");
        builder.Property(t => t.DueAt).HasColumnName("due_at");
        builder.Property(t => t.DueHasTime).HasColumnName("due_has_time").IsRequired();
        builder.Property(t => t.Priority).HasColumnName("priority").HasConversion<string>().HasMaxLength(10).IsRequired();
        builder.Property(t => t.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(t => t.CompletedAt).HasColumnName("completed_at");
        builder.Property(t => t.TimeZone).HasColumnName("time_zone").HasMaxLength(100).IsRequired();
        builder.Property(t => t.Rrule).HasColumnName("rrule").HasColumnType("text");
        builder.Property(t => t.Position).HasColumnName("position").IsRequired();
        builder.Property(t => t.DeletedAt).HasColumnName("deleted_at");

        // Declares the FKs so EF orders inserts and matches the migration's constraints.
        builder.HasOne<TaskList>()
               .WithMany()
               .HasForeignKey(t => t.ListId)
               .HasConstraintName("fk_agd005_list")
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<TaskItem>()
               .WithMany()
               .HasForeignKey(t => t.ParentTaskId)
               .HasConstraintName("fk_agd005_parent")
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(t => t.UserId).HasDatabaseName("ix_agd005_user_id");
        builder.HasIndex(t => new { t.ListId, t.Status }).HasDatabaseName("ix_agd005_list_status");
        builder.HasIndex(t => t.ParentTaskId)
               .HasFilter("parent_task_id IS NOT NULL")
               .HasDatabaseName("ix_agd005_parent_task_id");

        builder.Property(t => t.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(t => t.CreatedBy).HasColumnName("created_by");
        builder.Property(t => t.UpdatedAt).HasColumnName("updated_at");
        builder.Property(t => t.UpdatedBy).HasColumnName("updated_by");
    }
}
